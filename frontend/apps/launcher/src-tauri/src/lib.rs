// src-tauri/src/lib.rs
use std::path::{Path, PathBuf};
use tauri::Manager;
mod commands;
mod core;
use std::sync::Mutex;

#[cfg(windows)]
use crate::core::job_object::JobObject;

// 定义一个结构体来存储我们的应用级状态
// 这里我们存入应用的根目录路径
pub struct AppState {
    app_dir: PathBuf,
    #[cfg(windows)]
    pub job: Mutex<Option<JobObject>>,
}

impl AppState {
    /// 解析相对路径，并确保它安全地位于应用根目录内。
    /// 这是所有文件操作命令的安全基石。
    pub fn resolve_safe_path(&self, relative_path: &str) -> Result<PathBuf, String> {
        // 1. 检查危险的路径格式
        if relative_path.is_empty() || relative_path.contains("..") {
            return Err(format!(
                "Invalid or potentially malicious path specified: {}",
                relative_path
            ));
        }

        // 2. 将相对路径附加到安全的根目录
        let safe_path = self.app_dir.join(relative_path);

        // 3. 最终验证：确保最终路径确实以我们的根目录开头
        //    这是防止符号链接等更复杂攻击的最后一道防线。
        if !safe_path.starts_with(&self.app_dir) {
            return Err(format!(
                "Path traversal attempt detected: {}",
                relative_path
            ));
        }

        Ok(safe_path)
    }

    /// 检查给定的路径是否就是应用根目录。
    pub fn is_root(&self, path_to_check: &Path) -> bool {
        *path_to_check == self.app_dir
    }
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
            app.manage(AppState {
                app_dir: exe_dir,
                #[cfg(windows)]
                job: Mutex::new(None),
            });
            Ok(())
        })
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![
            commands::download::download_file,
            commands::unzip::unzip_file,
            commands::fs_utils::delete_file,
            commands::process::start_local_backend
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
