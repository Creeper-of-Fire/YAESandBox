use serde::{Deserialize, Serialize};
use tauri::command;

// 1. 定义与 JSON 结构匹配的 Rust 结构体
#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct PluginInfo {
    id: String,
    name: String,
    version: String,
    description: String,
    url: String,
}

#[command]
pub async fn fetch_plugins_manifest(
    url: String,
    proxy: Option<String>,
) -> Result<Vec<PluginInfo>, String> {
    println!("[Plugins] Fetching manifest from: {}", url);
    if let Some(p) = &proxy {
        println!("[Plugins] Using proxy: {}", p);
    }

    // 动态构建 reqwest 客户端
    let client_builder = reqwest::Client::builder();

    // 如果前端传入了代理地址，就配置代理
    let client = if let Some(proxy_url) = proxy {
        let proxy = reqwest::Proxy::all(&proxy_url)
            .map_err(|e| format!("Invalid proxy URL '{}': {}", proxy_url, e))?;
        client_builder.proxy(proxy).build()
    } else {
        client_builder.build()
    }
    .map_err(|e| format!("Failed to build HTTP client: {}", e))?;

    // 3. 使用构建好的 client 发起请求
    let response = client
        .get(&url)
        .send()
        .await
        .map_err(|e| format!("Failed to fetch manifest: {}", e))?;

    if !response.status().is_success() {
        return Err(format!(
            "Failed to fetch manifest: Server responded with {}",
            response.status()
        ));
    }

    let plugins = response
        .json::<Vec<PluginInfo>>()
        .await
        .map_err(|e| format!("Failed to parse manifest JSON: {}", e))?;

    println!(
        "[Plugins] Successfully fetched and parsed {} plugins.",
        plugins.len()
    );
    Ok(plugins)
}
