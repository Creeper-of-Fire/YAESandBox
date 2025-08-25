// src-tauri/src/commands/version_manager.rs
use crate::AppState;
use std::collections::HashMap;
use std::fs::{self, File};
use std::io::BufReader;
use std::path::PathBuf;
use tauri::{command, State};

// --- 数据结构 ---
// 直接使用 HashMap 的别名，更清晰。
type LocalVersionsMap = HashMap<String, String>;

// --- 辅助函数 ---

fn get_local_versions_path(app_state: &AppState) -> Result<PathBuf, String> {
    let app_root_dir = &app_state.app_dir;
    let versions_file_path = app_root_dir.join("local_versions.json");
    Ok(versions_file_path)
}

// --- Tauri 命令 ---

/// 获取本地安装的组件版本
#[command]
pub fn get_local_versions(app_state: State<'_, AppState>) -> Result<LocalVersionsMap, String> {
    let path = get_local_versions_path(app_state.inner())?;

    if !path.exists() {
        // ✨ 健壮性改进：如果文件不存在，这是首次运行。
        // 我们创建一个新的 map，将当前启动器的版本作为第一个条目写入文件。
        // 这是唯一一次我们依赖编译时版本，用于“初始化”。
        let mut versions = LocalVersionsMap::new();
        let initial_launcher_version = env!("CARGO_PKG_VERSION").to_string();
        versions.insert("launcher".to_string(), initial_launcher_version);

        // 将初始化的版本信息写回文件，建立“真相源”
        let content = serde_json::to_string_pretty(&versions)
            .map_err(|e| format!("初始化版本文件时序列化失败: {}", e))?;
        fs::write(&path, content).map_err(|e| format!("初始化版本文件时写入失败: {}", e))?;

        // 返回刚刚创建的 map
        return Ok(versions);
    }

    // 文件存在，直接读取并返回。不再用 env! 宏覆盖任何值。
    let file = File::open(&path).map_err(|e| format!("读取版本文件失败: {}", e))?;
    let reader = BufReader::new(file);
    let versions: LocalVersionsMap =
        serde_json::from_reader(reader).map_err(|e| format!("解析版本文件失败: {}", e))?;

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

    // 1. 获取当前的所有版本信息
    //    这里调用我们自己的 get_local_versions，它能处理文件不存在的初始情况。
    let mut versions = get_local_versions(app_state.clone())?;

    // 2. 使用 insert，动态地更新或添加任何组件的版本。
    versions.insert(component_id, new_version);

    // 3. 将更新后的完整版本信息写回文件
    let content = serde_json::to_string_pretty(&versions)
        .map_err(|e| format!("序列化版本信息失败: {}", e))?;
    fs::write(path, content).map_err(|e| format!("写入版本文件失败: {}", e))?;

    Ok(())
}
