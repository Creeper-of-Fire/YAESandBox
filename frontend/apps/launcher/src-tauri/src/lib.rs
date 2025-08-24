// src-tauri/src/lib.rs
use futures_util::StreamExt;
use serde::Serialize;
use std::io::Write;
use std::path::PathBuf;
use tauri::{command, Manager};
mod commands;

// 定义事件的载荷 (Payload) 结构体
// 这个结构体定义了我们发送给前端的数据格式
// `Serialize` 和 `Clone` 是必需的
#[derive(Clone, Serialize)]
struct DownloadProgress {
    downloaded: u64,
    total: Option<u64>,
}

// 定义一个结构体来存储我们的应用级状态
// 这里我们存入应用的根目录路径
pub struct AppState {
    app_dir: PathBuf,
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .setup(|app| {
            let exe_dir = std::env::current_exe()
                .ok()
                .and_then(|p| p.parent().map(|p| p.to_path_buf()))
                .expect("Failed to get exe parent directory");

            println!("App executable directory: {}", exe_dir.display());
            app.manage(AppState { app_dir: exe_dir });
            Ok(())
        })
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![
            commands::download::download_file
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
#[command]
fn greet(name: &str) -> String {
    format!("Hello, {}! You've been greeted from Rust!", name)
}
