// src-tauri/src/lib.rs
use std::path::{Path, PathBuf};
use tauri::Manager;
mod commands;
pub mod core;
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
            return Err(format!("指定的路径无效或存在潜在风险: {}", relative_path));
        }

        // 2. 将相对路径附加到安全的根目录
        let safe_path = self.app_dir.join(relative_path);

        // 3. 最终验证：确保最终路径确实以我们的根目录开头
        //    这是防止符号链接等更复杂攻击的最后一道防线。
        if !safe_path.starts_with(&self.app_dir) {
            return Err(format!("检测到路径遍历攻击: {}", relative_path));
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
            // 1. 获取可执行文件所在的目录
            let exe_dir = std::env::current_exe()
                .ok()
                .and_then(|p| p.parent().map(|p| p.to_path_buf()))
                .expect("未能获取可执行文件所在目录");

            // 2. 定义我们想要的 WebView2 缓存目录
            //    建议放在一个子文件夹内，保持整洁
            let data_dir = exe_dir.join("app_data");
            println!("应用数据和缓存目录设置为: {}", data_dir.display());

            // 3. 手动创建主窗口
            //    我们使用 tauri::webview::WebviewWindowBuilder，这是您在文档中找到的正确工具
            let _webview_window = tauri::webview::WebviewWindowBuilder::new(
                app.handle(),                                // 需要 AppHandle
                "main",                                      // 窗口的唯一标签 (label)
                tauri::WebviewUrl::App("index.html".into()), // 你的前端入口 HTML
            )
            .title("YAESandBox启动器") // 设置窗口标题
            .inner_size(800.0, 600.0) // 设置窗口大小
            .data_directory(data_dir) // 在这里设置数据/缓存目录
            .disable_drag_drop_handler() // 禁用窗口拖拽事件处理器，以使用html5原生拖拽
            .build()?;

            // 之前的 AppState 管理代码可以保留
            app.manage(AppState {
                app_dir: exe_dir,
                #[cfg(windows)]
                job: Mutex::new(None),
            });

            Ok(())
        })
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![
            commands::unzip::unzip_file,
            commands::fs_utils::delete_file,
            commands::process::start_local_backend,
            commands::config_cmd::read_config_as_string,
            commands::manifest::fetch_manifest,
            commands::updater_cmd::apply_launcher_self_update,
            commands::update_manager::get_local_versions,
            commands::download::download_and_verify_zip,
            commands::update_manager::update_local_version
        ])
        .run(tauri::generate_context!())
        .expect("运行 Tauri 应用时出错");
}
