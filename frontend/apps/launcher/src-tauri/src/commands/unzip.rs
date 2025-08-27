// src-tauri/src/commands/unzip.rs
use super::super::AppState;
use std::fs::{self, File};
use std::io;
use tauri::{command, State};
use zip::ZipArchive;
use crate::core::ownership;


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
        return Err(format!("Zip 文件未找到: {}", zip_path.display()));
    }
    if app_state.is_root(&target_dir) {
        return Err("不允许直接解压到应用根目录。".to_string());
    }

    // --- 2. 准备目录：业务流程 ---
    // a. 安全地清理任何旧的、属于我们的版本
    ownership::safe_remove_owned_directory(&target_dir)?;

    // b. 创建一个新的空目录
    ownership::create_owned_directory(&target_dir)?;

    // --- 3. 执行解压 ---
    let unzip_result: Result<(), String> = (|| {
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
        Ok(())
    })();


    // --- 4. 事务收尾 ---
    match unzip_result {
        Ok(_) => {
            // 解压成功，万事大吉。所有权已在解压前标记。
            println!("[Unzip] 文件解压成功，目录 '{}' 已是最新状态。", target_dir.display());
            Ok(())
        }
        Err(e) => {
            // 解压失败，回滚操作。
            // 因为 `create_owned_directory` 已经标记了所有权，
            // 所以我们现在必须使用 `safe_remove_owned_directory` 来清理。
            println!("[Unzip] 解压失败: {}. 正在回滚...", e);
            ownership::safe_remove_owned_directory(&target_dir)
                .map_err(|cleanup_err| format!("解压失败，并且回滚清理也失败了: {}", cleanup_err))?;
            Err(format!("解压失败: {}", e))
        }
    }
}
