// src-tauri/src/commands/config_cmd.rs

use tauri::{command, State};
use crate::AppState;
use std::fs;

// 将配置文件名定义为常量，方便管理
const CONFIG_FILENAME: &str = "launcher.config";

/// 读取配置文件的原始内容并作为字符串返回。
/// 如果配置文件不存在，会自动创建一个包含默认值和详细注释的新文件。
#[command]
pub fn read_config_as_string(app_state: State<'_, AppState>) -> Result<String, String> {
    // 解析出配置文件的完整路径
    let config_path = app_state.app_dir.join(CONFIG_FILENAME);

    // 检查文件是否存在
    if !config_path.exists() {
        println!("[Config] 配置文件不存在，正在于 '{}' 创建默认配置...", config_path.display());

        // 使用原始字符串字面量 (r#""#) 来创建多行字符串，非常清晰
        let default_content = r#"; --- YAESandBox 启动器配置 ---
; 本文件用于配置启动器的更新源和网络设置。
; 请不要随意修改，除非您知道您在做什么。

[Manifests]
; 清单文件 URL 指向包含所有可更新组件（如前端、后端、插件）信息的 JSON 文件。
; 使用 "latest" 通常是指向最新的稳定版本。
core_components_manifest_url = "https://github.com/Creeper-of-Fire/YAESandBox/releases/latest/download/core_components_manifest.json"
plugins_manifest_url = "https://github.com/Creeper-of-Fire/YAESandBox/releases/latest/download/plugins_manifest.json"

[Network]
; 后端服务监听的本地端口。
; 设置为 "auto" 将自动选择一个未被占用的端口（不推荐，会导致浏览器缓存失效）。
; 推荐使用一个固定的、不容易被其他程序占用的端口（例如 10000-65535 之间）。
backend_port = "60983"

; 网络代理设置。如果您的网络环境需要代理才能访问 GitHub，请在此处填写。
; 格式为: http://<ip>:<port> 或者 socks5://<ip>:<port>
; 例如: http://127.0.0.1:7890
; 如果不需要代理，请留空。
proxy_address = ""
"#;
        // 尝试写入文件，如果失败则返回一个描述性的错误
        fs::write(&config_path, default_content)
            .map_err(|e| format!("创建默认配置文件失败: {}", e))?;
    }

    // 无论文件是刚刚创建的还是本来就存在的，都读取其内容并返回
    fs::read_to_string(config_path)
        .map_err(|e| format!("读取配置文件失败: {}", e))
}

/// 将给定的字符串内容覆写到配置文件中。
#[command]
pub fn write_config_as_string(app_state: State<'_, AppState>, content: String) -> Result<(), String> {
    let config_path = app_state.app_dir.join(CONFIG_FILENAME);
    fs::write(&config_path, content)
        .map_err(|e| format!("写入配置文件 '{}' 失败: {}", config_path.display(), e))
}