/// 一个简单的函数，用于从 INI 格式的字符串中解析出指定 section 下的 key 对应的值。
///
/// # Arguments
/// * `content` - INI 文件的完整内容字符串。
/// * `section` - 要查找的节名，例如 "Network"。
/// * `key` - 要查找的键名，例如 "backend_port"。
///
/// # Returns
/// 如果找到，返回 `Some(String)`，否则返回 `None`。
pub fn get_ini_value(content: &str, section: &str, key: &str) -> Option<String> {
    let mut current_section = "";
    let formatted_section = format!("[{}]", section);

    for line in content.lines() {
        let trimmed_line = line.trim();
        // 跳过注释和空行
        if trimmed_line.starts_with(';') || trimmed_line.is_empty() {
            continue;
        }

        // 检查是否是节头
        if trimmed_line.starts_with('[') && trimmed_line.ends_with(']') {
            current_section = trimmed_line;
            continue;
        }

        // 如果在正确的节内，则查找键
        if current_section == formatted_section {
            if let Some((k, v)) = trimmed_line.split_once('=') {
                if k.trim() == key {
                    // 返回修剪并去掉引号（如果有）的值
                    return Some(v.trim().trim_matches('"').to_string());
                }
            }
        }
    }

    None
}