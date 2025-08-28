use std::fs;
use tauri::{command, State};
use crate::AppState;

// 定义日志文件名常量，确保与 main.rs 中一致
const LOG_FILENAME: &str = "launcher.log";

/// [Tauri Command] 读取日志文件的全部内容并作为字符串返回。
#[command]
pub fn get_log_content(app_state: State<'_, AppState>) -> Result<String, String> {
    // 1. 解析出日志文件的完整、安全路径
    let log_path = app_state.app_dir.join(LOG_FILENAME);

    // 2. 读取文件内容
    // 如果文件不存在或读取失败，返回一个描述性的错误给前端
    fs::read_to_string(log_path)
        .map_err(|e| format!("读取日志文件失败: {}", e))
}