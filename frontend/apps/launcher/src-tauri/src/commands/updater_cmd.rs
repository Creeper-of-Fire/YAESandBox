use std::env;
use std::fs;
use tauri::{command, AppHandle, State};
use serde::{Deserialize, Serialize};
use crate::AppState;
use crate::core::ownership;

const TEMP_UPDATE_DIR_NAME: &str = "_temp_updates";

// 一个简单的结构体，用于写入标记文件
#[derive(Serialize, Deserialize)]
struct UpdateMarker {
    version: String,
}

/// 执行更新流程，此函数现在假定 ZIP 文件已被下载和校验
#[command]
pub async fn apply_launcher_self_update(
    app_handle: AppHandle,
    app_state: State<'_, AppState>, // 依赖注入 AppState
    zip_relative_path: String,      // 接收 ZIP 文件的相对路径
    new_version: String,            // 需要更新本地版本记录
) -> Result<(), String> {
    // --- 步骤 1: 解析文件路径 ---
    let zip_path = app_state.resolve_safe_path(&zip_relative_path)?;
    if !zip_path.exists() {
        return Err(format!("启动器更新包未找到于: {}", zip_path.display()));
    }
    log::info!("[Updater] 找到更新包: {}", zip_path.display());

    // --- 步骤 2: 解压到临时目录 ---
    let temp_parent_dir = app_state.app_dir.join(TEMP_UPDATE_DIR_NAME);

    // a. 首先，安全地清理任何旧的、属于我们的临时目录
    ownership::safe_remove_owned_directory(&temp_parent_dir)?;

    // b. 然后，创建一个新的、带所有权的临时目录
    ownership::create_owned_directory(&temp_parent_dir)?;

    let temp_dir = tempfile::Builder::new()
        .prefix("launcher-update-")
        .tempdir_in(&temp_parent_dir)
        .map_err(|e| format!("在受控的临时目录内创建工作区失败: {}", e))?;

    log::info!("[Updater] 已在受控目录内创建临时工作区: {}", temp_dir.path().display());

    let bin_name = "yaesandbox-launcher.exe";

    // 我们从一个已知的、安全的本地文件路径来解压
    self_update::Extract::from_source(&zip_path)
        .archive(self_update::ArchiveKind::Zip)
        .extract_file(temp_dir.path(), bin_name)
        .map_err(|e| format!("从ZIP包解压 '{}' 失败: {}", bin_name, e))?;

    let new_exe_path = temp_dir.path().join(bin_name);
    if !new_exe_path.exists() {
        return Err(format!("解压后未在临时目录中找到 '{}'", bin_name));
    }
    log::info!("[Updater] 已成功解压新版本到: {}", new_exe_path.display());

    // --- 步骤 3: 创建“原子更新”标记文件 ---
    // 这是保证状态一致性的关键！
    let exe_dir = env::current_exe().map_err(|e| e.to_string())?.parent().unwrap().to_path_buf();
    let marker_path = exe_dir.join("update_pending.json");
    let marker = UpdateMarker { version: new_version };
    let marker_content = serde_json::to_string_pretty(&marker).unwrap();
    fs::write(&marker_path, marker_content).map_err(|e| format!("创建更新标记文件失败: {}", e))?;
    log::info!("[Updater] 已创建更新标记于: {}", marker_path.display());

    // --- 步骤 4: 调用 self_replace 执行魔法 ---
    // 这是最关键的一步。这个函数会处理所有平台相关的棘手问题。
    log::info!("[Updater] 准备执行自我替换...");
    self_update::self_replace::self_replace(&new_exe_path)
        .map_err(|e| format!("自我替换操作失败: {}", e))?;

    // 如果 self_replace 成功，当前进程已经被替换，但仍在运行旧代码。
    // 我们需要重启来加载新版本的代码。
    log::info!("[Updater] 替换成功！准备重启应用...");
    app_handle.restart();

    #[allow(unreachable_code)]
    Ok(())
}