// src-tauri/src/core/ownership.rs

use std::fs::{self, File};
use std::path::Path;

/// 用于标识启动器管理目录的标记文件名。
pub const MARKER_FILENAME: &str = ".launcher-managed";

/// **[Read] 验证**一个目录的所有权。
///
/// 检查指定的 `dir_path` 是否存在，并且是否包含所有权标记文件。
///
/// # Returns
/// - `Ok(true)`: 目录存在且包含标记文件。
/// - `Ok(false)`: 目录不存在，或存在但**不**包含标记文件。
/// - `Err(String)`: 路径存在但不是一个目录。
pub fn verify_ownership(dir_path: &Path) -> Result<bool, String> {
    if !dir_path.exists() {
        return Ok(false); // 目录不存在，自然没有所有权。
    }

    if !dir_path.is_dir() {
        return Err(format!(
            "路径 '{}' 已存在但不是一个目录。",
            dir_path.display()
        ));
    }

    let marker_path = dir_path.join(MARKER_FILENAME);
    Ok(marker_path.exists())
}

/// **[Create] 创建**一个目录并**立即标记**其所有权。
///
/// 如果目录已存在，此函数将返回错误，除非它已经归我们所有。
/// 这是一个“安全创建”操作。
///
/// # Logic
/// 1. 检查路径是否存在。
/// 2. 如果存在，必须验证所有权，否则失败。
/// 3. 如果不存在，则创建目录。
/// 4. 在新创建的目录中放置所有权标记。
pub fn create_owned_directory(dir_path: &Path) -> Result<(), String> {
    if dir_path.exists() {
        // 如果目录已存在，我们必须确认它是我们自己的，否则不允许操作
        match verify_ownership(dir_path)? {
            true => {
                println!("[Ownership] 目录 '{}' 已存在并已验证所有权，无需创建。", dir_path.display());
                return Ok(()); // 已经是我们的了，操作成功
            },
            false => return Err(format!(
                "创建失败：目录 '{}' 已存在且不由本启动器管理。",
                dir_path.display()
            )),
        }
    }

    // 目录不存在，创建它
    fs::create_dir_all(dir_path)
        .map_err(|e| format!("创建目录 '{}' 失败: {}", dir_path.display(), e))?;

    // 立即标记所有权
    mark_as_owned(dir_path)
}

/// **[Delete] 安全地移除**一个归启动器所有的目录。
///
/// 只有在 `verify_ownership` 返回 `Ok(true)` 的情况下才会执行删除。
/// 如果目录不存在或不归我们所有，则不执行任何操作并成功返回。
pub fn safe_remove_owned_directory(dir_path: &Path) -> Result<(), String> {
    match verify_ownership(dir_path)? {
        true => {
            // 所有权验证通过，执行删除
            println!("[Ownership] 正在安全地移除受控目录 '{}'...", dir_path.display());
            fs::remove_dir_all(dir_path)
                .map_err(|e| format!("移除受控目录 '{}' 失败: {}", dir_path.display(), e))
        },
        false => {
            if dir_path.exists() {
                println!("[Ownership] 跳过移除：目录 '{}' 不由本启动器管理。", dir_path.display());
            }
            Ok(()) // 目录不存在或不属于我们，都视为“删除”成功
        }
    }
}


/// **[Update] 标记**一个已存在的目录为启动器所有。
///
/// 这是一个独立的、明确的“更新”所有权状态的操作。
pub fn mark_as_owned(dir_path: &Path) -> Result<(), String> {
    if !dir_path.is_dir() {
        return Err(format!("无法标记 '{}'，因为它不是一个有效的目录。", dir_path.display()));
    }
    let marker_path = dir_path.join(MARKER_FILENAME);
    let marker_path_display = marker_path.display().to_string();
    File::create(&marker_path)
        .map_err(|e| format!("创建所有权标记文件 '{}' 失败: {}", marker_path_display, e))?;
    println!("[Ownership] 已成功为目录 '{}' 设置所有权标记。", dir_path.display());
    Ok(())
}