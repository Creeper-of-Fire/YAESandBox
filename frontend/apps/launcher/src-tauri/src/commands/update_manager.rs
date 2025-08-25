use crate::AppState;
use serde::{Deserialize, Serialize};
use std::fs::{self};
use std::path::PathBuf;
use tauri::{command, State};
// --- 数据结构 ---

#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct LocalVersions {
    pub app: Option<String>,
    pub backend: Option<String>,
    pub launcher: Option<String>,
}

// --- 辅助函数 ---

/// 获取 local_versions.json 的路径
fn get_local_versions_path(app_state: &AppState) -> Result<PathBuf, String> {
    // 直接使用 AppState 中已经解析好的、安全的应用根目录
    let app_root_dir = &app_state.app_dir;

    // 将版本文件名附加到根目录路径上
    let versions_file_path = app_root_dir.join("local_versions.json");

    Ok(versions_file_path)
}
// --- Tauri 命令 ---

/// 获取本地安装的组件版本
#[command]
pub fn get_local_versions(app_state: State<'_, AppState>) -> Result<LocalVersions, String> {
    let path = get_local_versions_path(app_state.inner())?;
    if !path.exists() {
        // 如果文件不存在，返回一个空的版本对象，表示所有组件都未安装
        return Ok(LocalVersions {
            app: None,
            backend: None,
            launcher: Some(env!("CARGO_PKG_VERSION").to_string()),
        });
    }
    let content = fs::read_to_string(path).map_err(|e| format!("读取版本文件失败: {}", e))?;
    let mut versions: LocalVersions =
        serde_json::from_str(&content).map_err(|e| format!("解析版本文件失败: {}", e))?;

    // 启动器版本总是以编译时为准，因为它是运行的本体
    versions.launcher = Some(env!("CARGO_PKG_VERSION").to_string());

    Ok(versions)
}

/// 更新本地版本记录文件
#[command]
pub fn update_local_version(
    app_state: State<'_, AppState>,
    component_id: String,
    new_version: String,
) -> Result<(), String> {
    let path = get_local_versions_path(app_state.inner())?;
    let mut versions = get_local_versions(app_state.clone()).unwrap_or(LocalVersions {
        app: None,
        backend: None,
        launcher: None,
    });

    match component_id.as_str() {
        "app" => versions.app = Some(new_version),
        "backend" => versions.backend = Some(new_version),
        // launcher 版本由 env macro 管理，此处不处理
        _ => return Err("未知的组件ID".into()),
    }

    let content = serde_json::to_string_pretty(&versions)
        .map_err(|e| format!("序列化版本信息失败: {}", e))?;
    fs::write(path, content).map_err(|e| format!("写入版本文件失败: {}", e))?;

    Ok(())
}
