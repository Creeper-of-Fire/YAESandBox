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
            .map_err(|e| format!("无效的代理 URL '{}': {}", proxy_url, e))?;
        client_builder.proxy(proxy).build()
    } else {
        client_builder.build()
    }
    .map_err(|e| format!("构建 HTTP 客户端失败: {}", e))?;

    // 3. 使用构建好的 client 发起请求
    let response = client
        .get(&url)
        .send()
        .await
        .map_err(|e| format!("获取清单失败: {}", e))?;

    if !response.status().is_success() {
        return Err(format!(
            "获取清单失败: 服务器响应状态码 {}",
            response.status()
        ));
    }

    let plugins = response
        .json::<Vec<PluginInfo>>()
        .await
        .map_err(|e| format!("解析清单 JSON 失败: {}", e))?;

    println!(
        "[Plugins] 成功获取并解析了 {} 个插件。",
        plugins.len()
    );
    Ok(plugins)
}
