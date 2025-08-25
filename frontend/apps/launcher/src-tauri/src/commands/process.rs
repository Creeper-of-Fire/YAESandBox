// src-tauri/src/commands/process.rs

use super::super::AppState;
use std::io::{BufRead, BufReader};
use std::process::{Command as StdCommand, Stdio};
use std::thread;
use std::time::Duration;
use tauri::{command, AppHandle, Emitter, Manager, State};
use tokio::sync::mpsc;
use tokio::time::timeout;

// 平台特定的引入
#[cfg(windows)]
use std::os::windows::process::CommandExt;
use crate::commands::config_cmd::read_config_as_string;
use crate::core::config_parser::get_ini_value;

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
        // 先调用 read_config_as_string 来确保配置文件存在并获取其内容
        let config_content = read_config_as_string(app_state.clone())?;
        // 解析 backend_port
        let port_str = get_ini_value(&config_content, "Network", "backend_port")
            .unwrap_or_else(|| "auto".to_string()); // 如果没找到，默认为 "auto"

        println!("[Launcher] 从配置中读取到 backend_port = '{}'", port_str);

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

    println!("[Launcher] 后端将启动于端口: {}", port);


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
    let mut child = command.spawn().map_err(|e| format!("启动后端进程失败: {}", e))?;
    let pid = child.id();

    // 在 Windows 上，将子进程的生命周期与父进程绑定
    #[cfg(windows)]
    {
        // 创建一个新的 Job Object
        let job = crate::core::job_object::JobObject::new()?;

        job.assign_process_by_pid(pid)?;

        println!("[Launcher] 后端进程已成功分配至作业对象。");

        // 将 Job Object 存储到 AppState 中，以保持其存活。
        // 当 AppState 被销毁时 (即启动器关闭时)，JobObject 的 Drop trait 会被调用，
        // 从而关闭内核句柄，并终止所有关联的进程。
        *app_state.job.lock().unwrap() = Some(job);
    }

    // --- 5. 异步处理子进程的 stdout 和 stderr ---
    let stdout = child.stdout.take().expect("未能捕获标准输出");
    let stderr = child.stderr.take().expect("未能捕获标准错误");

    let app_handle_clone = app_handle.clone();
    thread::spawn(move || {
        let reader = BufReader::new(stdout);
        for line in reader.lines().flatten() { // flatten() 忽略读取错误
            println!("[后端标准输出]: {}", line);
            let _ = app_handle_clone.emit("backend-log", &line);

            if line.contains("Now listening on") {
                let _ = tx.try_send(());
            }
        }
    });

    let app_handle_clone_err = app_handle.clone();
    thread::spawn(move || {
        let reader = BufReader::new(stderr);
        for line in reader.lines().flatten() {
            println!("[后端标准错误]: {}", line);
            let _ = app_handle_clone_err.emit("backend-log", &format!("[ERROR] {}", line));
        }
    });

    // --- 6. 等待“后端就绪”信号，并设置超时 ---
    let timeout_duration = Duration::from_secs(15);
    match timeout(timeout_duration, rx.recv()).await {
        Ok(Some(_)) => {
            println!("[Launcher] 后端已就绪。正在导航主窗口...");
            let main_window = app_handle.get_webview_window("main").unwrap();
            main_window.navigate(api_url.parse().unwrap())
                .map_err(|e| format!("导航窗口失败: {}", e))?;
            Ok(())
        }
        Ok(None) => Err("后端进程意外退出。".to_string()),
        Err(_) => Err(format!(
            "后端未能在 {} 秒内成功启动。",
            timeout_duration.as_secs()
        )),
    }
}