use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};
use serde::{Deserialize, Serialize};
use toml_edit::{value, DocumentMut, Item, Table};

/// 定义所有需要被迁移的、旧的注释符号。
const LEGACY_COMMENT_SYMBOLS: &[&str] = &[";"];
/// 定义 TOML 规范的标准注释符号。这是所有注释最终会被转换成的形式。
const TOML_COMMENT_SYMBOL: &str = "#";

const CONFIG_FILENAME: &str = "launcher.config";

// 这个结构体现在是前后端通信的唯一模型。
#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct ConfigEntry {
    pub section: String,
    pub key: String,
    pub value: String,
    pub comments: Vec<String>,
}

/// 定义一个配置项的完整默认信息，包括注释
struct ConfigDefault {
    section: &'static str,
    key: &'static str,
    value: &'static str,
    comments: &'static [&'static str],
}

const DEFAULT_CONFIG_START: &str = r#"# --- YAESandBox 启动器配置 ---
# 本文件用于配置启动器的更新源、网络和外观设置。
"#;

/// 所有默认配置项的静态数组。这是我们配置的唯一“真理之源”。
const DEFAULTS: &[ConfigDefault] = &[
    ConfigDefault {
        section: "Appearance",
        key: "theme",
        value: "auto",
        comments: &[
            "# 应用主题设置。",
            "# auto  = 跟随操作系统设置",
            "# light = 始终为浅色模式",
            "# dark  = 始终为深色模式",
        ],
    },
    ConfigDefault {
        section: "Manifests",
        key: "core_components_manifest_url",
        value: "https://github.com/Creeper-of-Fire/YAESandBox/releases/latest/download/core_components_manifest.json",
        comments: &[
            "# 核心组件清单文件 URL，指向包含启动器自身、前端、后端等更新信息的 JSON 文件。",
            "# 使用 \"latest\" 通常是指向最新的稳定版本。",
        ],
    },
    ConfigDefault {
        section: "Manifests",
        key: "plugins_manifest_url",
        value: "https://github.com/Creeper-of-Fire/YAESandBox/releases/latest/download/plugins_manifest.json",
        comments: &[
            "# 插件清单文件 URL，指向包含所有可选插件信息的 JSON 文件。",
            "# 这允许插件列表与核心组件分开维护和更新。",
        ],
    },
    ConfigDefault {
        section: "Network",
        key: "backend_port",
        value: "60983",
        comments: &[
            "# 后端服务监听的本地端口。",
            "# 设置为 \"auto\" 将自动选择一个未被占用的端口（不推荐，会导致浏览器缓存失效）。",
            "# 推荐使用一个固定的、不容易被其他程序占用的端口（例如 10000-65535 之间）。",
        ],
    },
    ConfigDefault {
        section: "Network",
        key: "proxy_address",
        value: "",
        comments: &[
            "# 网络代理设置。如果您的网络环境需要代理才能访问 GitHub，请在此处填写。",
            "# 格式为: http://<ip>:<port> 或者 socks5://<ip>:<port>",
            "# 例如: http://127.0.0.1:7890",
            "# 如果不需要代理，请留空。",
        ],
    },
];

/// 获取配置文件的完整路径，该路径与可执行文件位于同一目录。
fn get_config_path(app_dir: &Path) -> PathBuf {
    app_dir.join(CONFIG_FILENAME)
}

/// 将任何在 LEGACY_COMMENT_SYMBOLS 中定义的注释符号，在内存中转换为标准的 TOML 注释符号。
fn normalize_comments(content: &str) -> String {
    content
        .lines()
        .map(|line| {
            let trimmed_line = line.trim();
            // 遍历所有已知的遗留注释符号
            for &legacy_symbol in LEGACY_COMMENT_SYMBOLS {
                if trimmed_line.starts_with(legacy_symbol) {
                    // 找到一个匹配项，替换行首的第一个符号，然后立即返回处理后的行
                    return line.replacen(legacy_symbol, TOML_COMMENT_SYMBOL, 1);
                }
            }
            // 如果循环结束都没有找到任何遗留符号，则原样返回该行
            line.to_string()
        })
        .collect::<Vec<String>>()
        .join("\n")
}

/// 非破坏性地加载配置文件。如果文件或任何必需的键/节不存在，则创建/补全它们，并保留所有现有格式和注释。
pub fn load_or_initialize(app_dir: &Path) -> Result<DocumentMut, String> {
    let config_path = get_config_path(app_dir);
    let mut needs_save = false;

    // 1. 读取或创建初始内容
    let raw_content = if config_path.exists() {
        fs::read_to_string(&config_path).map_err(|e| format!("读取配置文件失败: {}", e))?
    } else {
        log::info!("[Config] 配置文件不存在，将创建一个全新的配置文件。");
        needs_save = true;
        // 文件不存在时，创建一个包含全局注释的空文档
        DEFAULT_CONFIG_START
        .to_string()
    };

    // 2. 在解析前规范化注释
    let normalized_content = normalize_comments(&raw_content);

    // 3. 错误恢复逻辑
    let mut doc = match normalized_content.parse::<DocumentMut>() {
        Ok(parsed_doc) => parsed_doc,
        Err(e) => {
            log::error!("[Config] 解析配置文件 '{}' 失败: {}. 文件可能已损坏。文件内容: {}", config_path.display(), e, normalized_content);
            // 创建一个带时间戳的备份文件名
            let timestamp = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_secs();
            let backup_path = config_path.with_extension(format!("config.bak.{}", timestamp));

            // 尝试重命名损坏的文件
            if let Err(rename_err) = fs::rename(&config_path, &backup_path) {
                log::error!("[Config] 备份损坏的配置文件失败: {}", rename_err);
                // 如果连重命名都失败了，就返回一个致命错误
                return Err(format!("配置文件已损坏且无法备份: {}", e));
            }

            log::info!("[Config] 已将损坏的配置文件备份至 '{}'。", backup_path.display());
            needs_save = true; // 标记需要保存一个新的默认文件
            // 返回一个全新的空文档，后续逻辑会用默认值填充它
            DocumentMut::new()
        }
    };

    // 4. 遍历所有默认项，检查并补全缺失的配置
    for default in DEFAULTS {
        // 确保节(table)存在
        if !doc.contains_table(default.section) {
            let mut new_table = Table::new();
            // 为新创建的节添加空行前缀，使其与上一节分隔
            new_table.set_implicit(true);
            doc[default.section] = Item::Table(new_table);
            needs_save = true;
            log::info!("[Config] 补全缺失的节: [{}]", default.section);
        }

        let section = doc[default.section].as_table_mut().unwrap();

        // 确保键存在
        if !section.contains_key(default.key) {

            let should_add_newline = !section.is_empty();

            // 1. 插入一个干净的值
            section.insert(default.key, value(default.value));

            // 2. 使用 get_key_value_mut 获取对 Key 和 Item 的可变引用
            //    unwrap() 是安全的，因为我们刚刚插入了它
            let (mut key_mut, _item_mut) = section.get_key_value_mut(default.key).unwrap();

            // 3. 构建注释字符串
            let mut prefix_string = String::new();
            // 如果节内已有内容，则添加一个空行分隔，否则不加
            if should_add_newline {
                prefix_string.push('\n');
            }
            for comment in default.comments {
                prefix_string.push_str(comment);
                prefix_string.push('\n');
            }

            // 4. 调用 Key 上的 leaf_decor_mut() 来设置前缀注释
            key_mut.leaf_decor_mut().set_prefix(prefix_string);

            needs_save = true;
        }
    }

    // 5. 如果文档被修改过，则写回文件
    if needs_save || raw_content != doc.to_string() {
        fs::write(&config_path, doc.to_string())
            .map_err(|e| format!("回写补全的配置失败: {}", e))?;
        log::info!("[Config] 已保存更新后的配置文件。");
    }

    Ok(doc)
}

/// 获取扁平化的配置项列表，用于发送给前端。
pub fn get_config_as_entries(app_dir: &Path) -> Result<Vec<ConfigEntry>, String> {
    let doc = load_or_initialize(app_dir)?;
    let mut entries = Vec::new();

    for default in DEFAULTS {
        // 从文档中读取当前值，如果不存在则使用默认值
        let current_value = doc
            .get(default.section)
            .and_then(|table| table.get(default.key))
            .and_then(|item| item.as_str())
            .unwrap_or(default.value)
            .to_string();

        entries.push(ConfigEntry {
            section: default.section.to_string(),
            key: default.key.to_string(),
            value: current_value,
            comments: default.comments.iter().map(|s| s.to_string()).collect(),
        });
    }

    Ok(entries)
}

/// 从接收扁平化的配置项列表，并保存到文件。
pub fn set_config_from_entries(app_dir: &Path, entries: &[ConfigEntry]) -> Result<(), String> {
    let mut doc = load_or_initialize(app_dir)?;

    for entry in entries {
        // 直接使用 section 和 key 更新文档
        doc[&entry.section][&entry.key] = value(entry.value.clone());
    }

    let config_path = get_config_path(app_dir);
    fs::write(config_path, doc.to_string())
        .map_err(|e| format!("从条目列表保存配置失败: {}", e))
}

/// 从文档中安全地获取一个字符串值
pub fn get_value(doc: &DocumentMut, section: &str, key: &str) -> Option<String> {
    doc.get(section)?.get(key)?.as_str().map(|s| s.to_string())
}

/// 安全地更新配置文件中的一个值并保存
pub fn set_value(
    app_dir: &Path,
    section: &str,
    key: &str,
    new_value: &str,
) -> Result<(), String> {
    let mut doc = load_or_initialize(app_dir)?; // 确保基于最新配置修改

    // toml_edit 的强大之处：直接赋值即可
    doc[section][key] = value(new_value);

    let config_path = get_config_path(app_dir);
    fs::write(config_path, doc.to_string())
        .map_err(|e| format!("保存配置项 {}/{} 失败: {}", section, key, e))
}

/// 获取配置文件的字符串表示形式
pub fn get_document_as_string(app_dir: &Path) -> Result<String, String> {
    let doc = load_or_initialize(app_dir)?;
    Ok(doc.to_string())
}