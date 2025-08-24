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

    // 如果前端传入了代理地址，就配置代理
    let client = crate::core::http::create_http_client(proxy.as_deref())?;

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
