use tauri::{command, AppHandle};

#[command]
pub async fn restart_application(app_handle: AppHandle) {
    log::info!("[Command] 接到前端重启请求，正在重启应用...");
    app_handle.restart();
}