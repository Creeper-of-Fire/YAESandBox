use serde_json::Value; // 只需要引入 Value
use tauri::command;

/// 通用的清单获取函数。
/// 从指定的 URL 下载一个 JSON 文件，并将其作为通用的 JSON Value 返回。
///
/// # Arguments
/// * `url` - 要获取的清单文件的 URL。
/// * `proxy` - 可选的代理服务器地址。
///
/// # Returns
/// * `Result<Value, String>` - 成功时返回解析后的 `serde_json::Value`，失败时返回错误信息字符串。
#[command]
pub async fn fetch_manifest(url: String, proxy: Option<String>) -> Result<Value, String> {
    // 增加统一的日志输出，方便调试
    println!("[Manifest] 正在从以下地址获取清单: {}", &url);

    // 1. 创建 HTTP 客户端，逻辑完全一致
    let client = crate::core::http::create_http_client(proxy.as_deref())?;

    // 2. 发起请求
    let response = client
        .get(&url)
        .send()
        .await
        .map_err(|e| format!("请求清单失败: {}", e))?;

    // 3. 检查服务器响应状态，这是一个好习惯
    if !response.status().is_success() {
        return Err(format!(
            "获取清单失败: 服务器响应状态码 {}",
            response.status()
        ));
    }

    // 4. 直接将响应体解析为 `serde_json::Value`
    //    这比 .text().await 然后 from_str() 更直接高效
    let manifest_json = response
        .json::<Value>()
        .await
        .map_err(|e| format!("解析清单 JSON 失败: {}", e))?;

    println!("[Manifest] 成功获取并解析了清单。");

    // 5. 返回通用的 JSON Value
    Ok(manifest_json)
}