// src-tauri/src/commands/process.rs
use std::sync::{Arc, Mutex};
use super::super::AppState;
use crate::core::config;
use std::io::{BufRead, BufReader};
use std::process::{Command as StdCommand, Stdio};
use std::thread;
use std::time::Duration;
use tauri::{command, AppHandle, Emitter, Manager, State};
use tokio::sync::mpsc;
use tokio::time::timeout;
use crate::core::dialog_window::show_critical_error_and_exit;

// 平台特定的引入
#[cfg(windows)]
use std::os::windows::process::CommandExt;

#[cfg(windows)]
const CREATE_NO_WINDOW: u32 = 0x08000000;


#[command]
pub async fn start_local_backend(
    frontend_relative_path: String,
    backend_exe_relative_path: String,
    app_handle: AppHandle,
    app_state: State<'_, AppState>,
) -> Result<(), String> {
    // --- 1. 准备环境 ---
    let port = {
        // a. 使用配置模块加载配置文档。
        //    这确保了配置文件是经过初始化和验证的。
        let doc = config::load_or_initialize(&app_state.app_dir)?;

        // b. 从文档中安全地获取 'backend_port' 的值。
        //    如果键、节不存在，或值不是字符串，则安全地回退到 "auto"。
        let port_str = doc
            .get("Network")
            .and_then(|table| table.get("backend_port"))
            .and_then(|item| item.as_str())
            .unwrap_or("auto")
            .to_string();

        log::info!("[Launcher] 从配置中读取到 backend_port = '{}'", port_str);

        if port_str.to_lowercase() == "auto" {
            // 行为不变：自动选择端口
            portpicker::pick_unused_port().expect("未能找到可用端口")
        } else {
            // 尝试解析为 u16 端口号
            match port_str.parse::<u16>() {
                Ok(p) => {
                    // 检查端口是否被占用 (可选但推荐)
                    if portpicker::is_free(p) {
                        p
                    } else {
                        // 如果被占用，返回错误，让用户去解决
                        return Err(format!(
                            "配置的端口 {} 已被占用。请关闭占用该端口的程序，或在 launcher.config 文件中修改端口。",
                            p
                        ));
                    }
                },
                Err(_) => {
                    // 解析失败，返回错误
                    return Err(format!(
                        "无效的端口号 '{}'。请在 launcher.config 文件中输入一个有效的端口号 (1-65535) 或 'auto'。",
                        port_str
                    ));
                }
            }
        }
    };

    log::info!("[Launcher] 后端将启动于端口: {}", port);


    let api_url = format!("http://127.0.0.1:{}", port);

    let backend_exe_path = app_state.resolve_safe_path(&backend_exe_relative_path)?;

    let backend_working_dir = backend_exe_path.parent().ok_or_else(||
        "无效的后端可执行文件路径: 无法确定父目录。".to_string()
    )?;

    // --- 2. 创建用于“后端就绪”信令的通道 ---
    let (tx, mut rx) = mpsc::channel::<()>(1);

    // --- 3. 配置子进程命令 ---
    let mut command = StdCommand::new(&backend_exe_path);
    command.current_dir(backend_working_dir);
    command.args(&[
        "--urls", &api_url,
        "--FrontendRelativePath", &frontend_relative_path,
    ]);
    command.stdout(Stdio::piped());
    command.stderr(Stdio::piped());

    #[cfg(windows)]
    command.creation_flags(CREATE_NO_WINDOW);

    // --- 启动子进程并将其绑定到 Job Object ---
    let mut child = match command.spawn() {
        Ok(child) => child,
        Err(e) => {
            let error_msg = format!("启动后端进程失败: {}\n\n请确认后端可执行文件存在且未被杀毒软件拦截。", e);
            // 直接调用我们的新函数！
            show_critical_error_and_exit("启动器错误", &error_msg);
            // 这行代码永远不会被执行，因为 show_critical_error_and_exit 会终止进程
            unreachable!();
        }
    };

    let pid = child.id();

    // 在 Windows 上，将子进程的生命周期与父进程绑定
    #[cfg(windows)]
    {
        // 创建一个新的 Job Object
        let job = crate::core::job_object::JobObject::new()?;

        job.assign_process_by_pid(pid)?;

        log::info!("[Launcher] 后端进程已成功分配至作业对象。");

        // 将 Job Object 存储到 AppState 中，以保持其存活。
        // 当 AppState 被销毁时 (即启动器关闭时)，JobObject 的 Drop trait 会被调用，
        // 从而关闭内核句柄，并终止所有关联的进程。
        *app_state.job.lock().unwrap() = Some(job);
    }

    // --- 5. 异步处理子进程的 stdout 和 stderr ---
    let stdout = child.stdout.take().expect("未能捕获标准输出");
    let stderr = child.stderr.take().expect("未能捕获标准错误");

    let stderr_lines = Arc::new(Mutex::new(Vec::<String>::new()));

    let app_handle_clone = app_handle.clone();
    thread::spawn(move || {
        let reader = BufReader::new(stdout);
        for line in reader.lines().flatten() { // flatten() 忽略读取错误
            log::info!("[后端标准输出]: {}", line);
            let _ = app_handle_clone.emit("backend-log", &line);

            if line.contains("Now listening on") {
                let _ = tx.try_send(());
            }
        }
    });

    let app_handle_clone_err = app_handle.clone();
    let stderr_lines_clone = stderr_lines.clone();
    thread::spawn(move || {
        let reader = BufReader::new(stderr);
        for line in reader.lines().flatten() {
            log::info!("[后端标准错误]: {}", line);
            let _ = app_handle_clone_err.emit("backend-log", &format!("[ERROR] {}", line));

            // 将错误行存入共享的 Vec
            let mut lines = stderr_lines_clone.lock().unwrap();
            lines.push(line);
        }
    });

    // --- 6. 等待“后端就绪”信号，并设置超时 ---
    let timeout_duration = Duration::from_secs(15);
    match timeout(timeout_duration, rx.recv()).await {
        Ok(Some(_)) => {
            log::info!("[Launcher] 后端已就绪。正在导航主窗口...");
            let main_window = app_handle.get_webview_window("main").unwrap();

            // 在导航之前，先最大化窗口。
            // maximize() 返回一个 Result，我们用 .ok() 忽略可能的错误，
            // 因为即使最大化失败，我们仍然希望继续导航。
            main_window.maximize().ok();

            main_window.navigate(api_url.parse().unwrap())
                .map_err(|e| format!("导航窗口失败: {}", e))?;
            Ok(())
        }
        Ok(None) => {
            let collected_errors = stderr_lines.lock().unwrap().join("\n");
            let error_msg = if collected_errors.is_empty() {
                "后端进程意外退出，但未提供任何错误信息。这可能由环境问题（如缺少.NET运行时）或程序崩溃引起。".to_string()
            } else {
                format!("后端进程启动失败并提前退出。错误详情：\n\n{}", collected_errors)
            };
            show_critical_error_and_exit("后端启动失败", &error_msg);
            unreachable!();
        },
        Err(_) => {
            let collected_errors = stderr_lines.lock().unwrap().join("\n");
            let error_msg = if collected_errors.is_empty() {
                format!("后端未能在 {} 秒内响应。请检查日志或尝试重启。", timeout_duration.as_secs())
            } else {
                format!(
                    "后端未能在 {} 秒内响应。捕获到的错误信息如下：\n\n{}",
                    timeout_duration.as_secs(),
                    collected_errors
                )
            };
            show_critical_error_and_exit("后端启动超时", &error_msg);
            unreachable!();
        },
    }
}