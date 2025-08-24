// src-tauri/src/core/http.rs

/// 创建一个配置了可选代理的 reqwest 客户端。
/// 这是所有需要发起网络请求的 command 的共享函数。
pub fn create_http_client(proxy: Option<&str>) -> Result<reqwest::Client, String> {
    let mut client_builder = reqwest::Client::builder();

    if let Some(proxy_url) = proxy {
        // 只有在代理地址非空时才进行配置
        if !proxy_url.trim().is_empty() {
            println!("[HTTP] 使用代理: {}", proxy_url);
            let proxy = reqwest::Proxy::all(proxy_url)
                .map_err(|e| format!("无效的代理 URL '{}': {}", proxy_url, e))?;
            client_builder = client_builder.proxy(proxy);
        }
    }

    client_builder
        .build()
        .map_err(|e| format!("构建 HTTP 客户端失败: {}", e))
}