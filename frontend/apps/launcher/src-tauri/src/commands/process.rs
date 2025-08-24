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
    let port = portpicker::pick_unused_port().expect("Failed to find a free port");
    let api_url = format!("http://127.0.0.1:{}", port);

    let backend_exe_path = app_state.resolve_safe_path(&backend_exe_relative_path)?;

    let backend_working_dir = backend_exe_path.parent().ok_or_else(||
        "Invalid backend executable path: cannot determine parent directory.".to_string()
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
    let mut child = command.spawn().map_err(|e| format!("Failed to spawn backend: {}", e))?;
    let pid = child.id();

    // 在 Windows 上，将子进程的生命周期与父进程绑定
    #[cfg(windows)]
    {
        // 创建一个新的 Job Object
        let job = crate::core::job_object::JobObject::new()?;

        job.assign_process_by_pid(pid)?;

        println!("[Launcher] Backend process successfully assigned to Job Object.");

        // 将 Job Object 存储到 AppState 中，以保持其存活。
        // 当 AppState 被销毁时 (即启动器关闭时)，JobObject 的 Drop trait 会被调用，
        // 从而关闭内核句柄，并终止所有关联的进程。
        *app_state.job.lock().unwrap() = Some(job);
    }

    // --- 5. 异步处理子进程的 stdout 和 stderr ---
    let stdout = child.stdout.take().expect("Failed to capture stdout");
    let stderr = child.stderr.take().expect("Failed to capture stderr");

    let app_handle_clone = app_handle.clone();
    thread::spawn(move || {
        let reader = BufReader::new(stdout);
        for line in reader.lines().flatten() { // flatten() 忽略读取错误
            println!("[Backend STDOUT]: {}", line);
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
            println!("[Backend STDERR]: {}", line);
            let _ = app_handle_clone_err.emit("backend-log", &format!("[ERROR] {}", line));
        }
    });

    // --- 6. 等待“后端就绪”信号，并设置超时 ---
    let timeout_duration = Duration::from_secs(15);
    match timeout(timeout_duration, rx.recv()).await {
        Ok(Some(_)) => {
            println!("[Launcher] Backend is ready. Navigating main window...");
            let main_window = app_handle.get_webview_window("main").unwrap();
            main_window.navigate(api_url.parse().unwrap())
                .map_err(|e| format!("Failed to navigate window: {}", e))?;
            Ok(())
        }
        Ok(None) => Err("Backend process exited prematurely.".to_string()),
        Err(_) => Err(format!(
            "Backend failed to start within {} seconds.",
            timeout_duration.as_secs()
        )),
    }
}