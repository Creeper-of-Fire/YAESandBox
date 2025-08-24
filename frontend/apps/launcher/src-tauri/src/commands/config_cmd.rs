// src-tauri/src/commands/config_cmd.rs

use tauri::{command, State};
use crate::AppState;
use std::fs;

const CONFIG_FILENAME: &str = "launcher.config";

/// 读取配置文件的原始内容并作为字符串返回。
#[command]
pub fn read_config_as_string(app_state: State<'_, AppState>) -> Result<String, String> {
    let config_path = app_state.app_dir.join(CONFIG_FILENAME);

    // 如果文件不存在，就地创建一个默认的。
    if !config_path.exists() {
        let default_content = r#"; --- YAESandBox 启动器配置 ---
[Downloads]
app_url = "https://.../app.zip"
backend_url = "https://.../backend.zip"
plugins_manifest_url = "https://.../plugins.json"
[Network]
proxy = ""
"#;
        fs::write(&config_path, default_content)
            .map_err(|e| format!("创建默认配置文件失败: {}", e))?;
    }

    fs::read_to_string(config_path)
        .map_err(|e| format!("读取配置文件失败: {}", e))
}