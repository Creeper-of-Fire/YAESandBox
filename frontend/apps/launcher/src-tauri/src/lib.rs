// src-tauri/src/lib.rs
use std::path::{Path, PathBuf};
use tauri::{Manager, WebviewUrl};
mod commands;
pub mod core;
use std::sync::Mutex;
use tauri::menu::{Menu, MenuItemBuilder, SubmenuBuilder};
use tauri::{AppHandle, Wry};

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

// show_log_viewer 函数保持不变，它已经很好了
fn show_log_viewer(app_handle: &AppHandle) {
    let log_window_label = "log_viewer";
    if let Some(window) = app_handle.get_webview_window(log_window_label) {
        window.set_focus().unwrap();
    } else {
        let empty_menu = Menu::new(app_handle).expect("无法创建空菜单");
        tauri::webview::WebviewWindowBuilder::new(
            app_handle,
            log_window_label,
            WebviewUrl::App("log_viewer.html".into()),
        )
        .title("启动器日志")
        .inner_size(700.0, 500.0)
        .menu(empty_menu)
        .build()
        .unwrap();
    }
}

/// 创建应用菜单
fn create_app_menu(app_handle: &AppHandle) -> tauri::Result<Menu<Wry>> {
    let handle = app_handle;

    // 创建“文件”子菜单
    let file_menu = SubmenuBuilder::new(handle, "文件").close_window().build()?;

    // 创建“视图”子菜单
    let toggle_log_item = MenuItemBuilder::new("显示开发者日志")
        .id("toggle-log-view")
        .build(handle)?;

    let devtools_item = MenuItemBuilder::new("打开开发者工具")
        .id("devtools")
        .accelerator("F12")
        .build(handle)?;

    let view_menu = SubmenuBuilder::new(handle, "视图")
        .item(&toggle_log_item)
        .separator()
        .item(&devtools_item)
        .build()?;

    // 组装并返回菜单
    Menu::with_items(handle, &[&file_menu, &view_menu])
}


#[tauri::command]
async fn show_app_menu(window: tauri::Window) {
    // 这个 command 的作用就是弹出主菜单
    if let Some(menu) = window.menu() {
        // 使用 popup_menu_at 来更好地控制位置
        // 0,0 坐标相对于窗口左上角，正好在我们的按钮下方
        window.popup_menu_at(&menu, tauri::Position::Logical(tauri::LogicalPosition{ x: 0.0, y: 0.0 })).unwrap();
    } else {
        log::warn!("尝试显示菜单，但窗口没有附加菜单。");
    }
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let builder = tauri::Builder::default()
        .setup(|app| {
            // 1. 获取可执行文件所在的目录
            let exe_dir = std::env::current_exe()
                .ok()
                .and_then(|p| p.parent().map(|p| p.to_path_buf()))
                .expect("未能获取可执行文件所在目录");

            // 2. 定义我们想要的 WebView2 缓存目录
            //    建议放在一个子文件夹内，保持整洁
            let data_dir = exe_dir.join("app_data");
            log::info!("应用数据和缓存目录设置为: {}", data_dir.display());

            let menu = create_app_menu(app.handle())?;

            // 3. 手动创建主窗口
            //    我们使用 tauri::webview::WebviewWindowBuilder，这是您在文档中找到的正确工具
            let _main_window = tauri::webview::WebviewWindowBuilder::new(
                app.handle(),                                // 需要 AppHandle
                "main",                                      // 窗口的唯一标签 (label)
                tauri::WebviewUrl::App("index.html".into()), // 你的前端入口 HTML
            )
            .title("YAESandBox启动器") // 设置窗口标题
            .inner_size(800.0, 600.0) // 设置窗口大小
            .data_directory(data_dir) // 在这里设置数据/缓存目录
            .disable_drag_drop_handler() // 禁用窗口拖拽事件处理器，以使用html5原生拖拽
            .devtools(true) //启用开发者工具
            .menu(menu)
            .on_menu_event(|window, event| {
                match event.id.as_ref() {
                    "toggle-log-view" => {
                        log::info!("[Menu] 切换日志视图事件触发");
                        // `window.app_handle()` 可以获取到 AppHandle
                        show_log_viewer(window.app_handle());
                    }
                    "devtools" => {
                        // 1. 获取 AppHandle
                        let app = window.app_handle();
                        // 2. 获取窗口标签
                        let label = window.label();
                        // 3. 使用标签获取 WebviewWindow 实例
                        if let Some(webview_window) = app.get_webview_window(label) {
                            // 4. 在 WebviewWindow 上调用 open_devtools
                            webview_window.open_devtools();
                        }
                    }
                    _ => {}
                }
            })
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
        .plugin(tauri_plugin_fs::init())
        .invoke_handler(tauri::generate_handler![
            show_app_menu,
            commands::unzip::unzip_file,
            commands::fs_utils::delete_file,
            commands::process::start_local_backend,
            commands::config_cmd::read_config_as_string,
            commands::config_cmd::write_config_as_string,
            commands::manifest::fetch_manifest,
            commands::updater_cmd::apply_launcher_self_update,
            commands::update_manager::get_local_versions,
            commands::download::download_and_verify_zip,
            commands::update_manager::update_local_version,
            commands::log_cmd::get_log_content
        ]);

    builder
        .run(tauri::generate_context!())
        .expect("运行 Tauri 应用时出错");
}
