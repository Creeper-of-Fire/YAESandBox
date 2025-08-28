use super::super::AppState;
use futures_util::StreamExt;
use serde::Serialize;
use std::fs;
use std::fs::File;
use std::io::Write;
use tauri::{command, AppHandle, Emitter, State};
// 使用 super 关键字引用父模块的 AppState

#[derive(Clone, Serialize)]
pub struct DownloadProgress {
    id: String, // 添加一个ID，用于前端区分是哪个文件在下载
    downloaded: u64,
    total: Option<u64>,
}

/// 下载文件，报告进度，并在完成后进行 SHA256 校验
#[command]
pub async fn download_and_verify_zip(
    id: String, // 用于进度报告的ID
    url: String,
    relative_path: String,
    expected_hash: String,
    proxy: Option<String>,
    app_handle: AppHandle,
    app_state: State<'_, AppState>,
) -> Result<(), String> {
    let target_path = app_state.resolve_safe_path(&relative_path)?;
    // 确保目录存在
    if let Some(parent) = target_path.parent() {
        fs::create_dir_all(parent).map_err(|e| format!("创建目录失败: {}", e))?;
    }

    let client = crate::core::http::create_http_client(proxy.as_deref())?;
    let response = client
        .get(&url)
        .send()
        .await
        .map_err(|e| format!("请求失败: {}", e))?;

    let total_size = response.content_length();
    let mut file = File::create(&target_path).map_err(|e| format!("创建文件失败: {}", e))?;
    let mut downloaded: u64 = 0;
    let mut stream = response.bytes_stream();
    let mut buffer = Vec::new();

    while let Some(item) = stream.next().await {
        let chunk = item.map_err(|e| format!("读取数据块失败: {}", e))?;
        buffer.extend_from_slice(&chunk);
        downloaded += chunk.len() as u64;
        app_handle
            .emit(
                "download-progress",
                DownloadProgress {
                    id: id.clone(),
                    downloaded,
                    total: total_size,
                },
            )
            .unwrap();
    }

    // 下载完成后写入文件
    file.write_all(&buffer)
        .map_err(|e| format!("写入文件失败: {}", e))?;

    // 开始校验
    log::info!(
        "[Updater] 下载完成，正在校验文件: {}",
        target_path.display()
    );
    use sha2::{Digest, Sha256};
    let mut hasher = Sha256::new();
    hasher.update(&buffer);
    let calculated_hash = hex::encode(hasher.finalize());

    if calculated_hash.to_lowercase() != expected_hash.to_lowercase() {
        // 校验失败，删除下载的文件
        fs::remove_file(&target_path).ok();
        return Err(format!(
            "文件校验失败！期望哈希: {}, 计算哈希: {}",
            expected_hash, calculated_hash
        ));
    }

    log::info!("[Updater] 文件校验成功。");
    Ok(())
}
