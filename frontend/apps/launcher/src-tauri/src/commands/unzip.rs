// src-tauri/src/commands/unzip.rs
use super::super::AppState;
use std::fs::{self, File};
use std::io;
use tauri::{command, State};
use zip::ZipArchive;
// 引用 lib.rs 中的 AppState

const MARKER_FILENAME: &str = ".launcher-managed";

#[command]
pub async fn unzip_file(
    zip_relative_path: String,
    target_relative_dir: String,
    app_state: State<'_, AppState>,
) -> Result<(), String> {
    // --- 1. 路径解析与基础安全检查 ---
    let zip_path = app_state.resolve_safe_path(&zip_relative_path)?;
    let target_dir = app_state.resolve_safe_path(&target_relative_dir)?;

    if !zip_path.exists() {
        return Err(format!("Zip file not found at: {}", zip_path.display()));
    }
    if app_state.is_root(&target_dir) {
        return Err("Unzipping directly into the application root is not allowed.".to_string());
    }

    // --- 2. 核心安全逻辑：所有权验证 ---
    if target_dir.exists() {
        let marker_path = target_dir.join(MARKER_FILENAME);
        if !marker_path.exists() {
            // 目录存在，但标记文件不存在 -> 立即中止！
            return Err(format!(
                "Directory '{}' already exists and is not managed by this launcher. Please rename or remove it manually.",
                target_dir.display()
            ));
        }
        // 标记文件存在，我们可以安全地删除旧目录
        fs::remove_dir_all(&target_dir)
            .map_err(|e| format!("Failed to remove old directory: {}", e))?;
    }

    // --- 3. 执行解压 ---
    fs::create_dir_all(&target_dir)
        .map_err(|e| format!("Failed to create target directory: {}", e))?;

    let file = File::open(&zip_path).map_err(|e| e.to_string())?;
    let mut archive = ZipArchive::new(file).map_err(|e| e.to_string())?;

    for i in 0..archive.len() {
        let mut file = archive.by_index(i).map_err(|e| e.to_string())?;
        let outpath = match file.enclosed_name() {
            Some(path) => target_dir.join(path),
            None => continue,
        };

        if (*file.name()).ends_with('/') {
            fs::create_dir_all(&outpath).map_err(|e| e.to_string())?;
        } else {
            if let Some(p) = outpath.parent() {
                if !p.exists() {
                    fs::create_dir_all(&p).map_err(|e| e.to_string())?;
                }
            }
            let mut outfile = File::create(&outpath).map_err(|e| e.to_string())?;
            io::copy(&mut file, &mut outfile).map_err(|e| e.to_string())?;
        }
    }

    // --- 4. 解压成功后，放置新的标记文件 ---
    let new_marker_path = target_dir.join(MARKER_FILENAME);
    File::create(new_marker_path)
        .map_err(|e| format!("Failed to create ownership marker: {}", e))?;

    Ok(())
}
