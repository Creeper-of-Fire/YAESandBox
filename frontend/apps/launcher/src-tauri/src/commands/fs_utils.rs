// src-tauri/src/commands/fs_utils.rs
use std::fs;
use tauri::{command, State};
use super::super::AppState;

#[command]
pub fn delete_file(
    relative_path: String,
    app_state: State<'_, AppState>,
) -> Result<(), String> {
    // --- 1. 路径解析与安全检查 ---
    let target_path = app_state.resolve_safe_path(&relative_path)?;

    if app_state.is_root(&target_path) {
        return Err("不能删除根目录。".into());
    }

    // --- 2. 检查文件是否存在并删除 ---
    if target_path.exists() {
        if target_path.is_file() {
            fs::remove_file(&target_path)
                .map_err(|e| format!("删除文件失败: {}", e))?;
            log::info!("成功删除文件: {}", target_path.display());
        } else {
            // 为了安全，这个 command 只删除文件，不删除目录
            return Err("目标路径是一个目录，不是文件。".into());
        }
    } else {
        // 文件不存在也算“成功”，因为最终状态是一样的
        log::info!("文件未找到，无需删除: {}", target_path.display());
    }

    Ok(())
}