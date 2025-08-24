use super::super::AppState;
use futures_util::StreamExt;
use serde::Serialize;
use std::fs::File;
use std::io::Write;
use tauri::{command, AppHandle, Emitter, State};
// 使用 super 关键字引用父模块的 AppState

#[derive(Clone, Serialize)]
pub struct DownloadProgress {
    downloaded: u64,
    total: Option<u64>,
}

#[command]
pub async fn download_file(
    // 标记为 pub
    url: String,
    relative_path: String,
    proxy: Option<String>,
    app_handle: AppHandle,
    app_state: State<'_, AppState>,
) -> Result<(), String> {
    let target_path = app_state.resolve_safe_path(&relative_path)?;

    let mut client_builder = reqwest::Client::builder();
    if let Some(proxy_url) = proxy {
        let proxy = reqwest::Proxy::all(&proxy_url)
            .map_err(|e| format!("Invalid proxy URL '{}': {}", proxy_url, e))?;
        client_builder = client_builder.proxy(proxy);
    }
    let client = client_builder
        .build()
        .map_err(|e| format!("Failed to build HTTP client: {}", e))?;
    let response = client
        .get(&url)
        .send()
        .await
        .map_err(|e| format!("Request failed: {}", e))?;

    let total_size = response.content_length();
    let mut file =
        File::create(&target_path).map_err(|e| format!("Failed to create file: {}", e))?;
    let mut downloaded: u64 = 0;
    let mut stream = response.bytes_stream();
    while let Some(item) = stream.next().await {
        let chunk = item.map_err(|e| format!("Failed to read chunk: {}", e))?;
        file.write_all(&chunk)
            .map_err(|e| format!("Failed to write to file: {}", e))?;
        downloaded += chunk.len() as u64;
        app_handle
            .emit(
                "download-progress",
                DownloadProgress {
                    downloaded,
                    total: total_size,
                },
            )
            .unwrap();
    }
    Ok(())
}
