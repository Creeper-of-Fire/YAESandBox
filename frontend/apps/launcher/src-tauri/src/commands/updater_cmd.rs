use std::env;
use std::fs;
use std::fs::File;
use std::io::Write;
use tauri::{command, AppHandle};
use serde::{Deserialize, Serialize};

// 一个简单的结构体，用于写入标记文件
#[derive(Serialize, Deserialize)]
struct UpdateMarker {
    version: String,
}

/// 执行更新流程
#[command]
pub async fn apply_launcher_self_update(
    app_handle: AppHandle,
    url: String,
    hash: String,
    proxy: Option<String>,
    new_version: String,
) -> Result<(), String> {
    // --- 步骤 1: 带代理的下载 ---
    println!("[Updater] 准备下载，代理: {:?}", proxy);
    let client_builder = reqwest::Client::builder();
    let client = if let Some(p) = proxy {
        let proxy = reqwest::Proxy::all(p).map_err(|e| format!("无效的代理地址: {}", e))?;
        client_builder.proxy(proxy).build().map_err(|e| e.to_string())?
    } else {
        client_builder.build().map_err(|e| e.to_string())?
    };

    let response = client.get(&url).send().await.map_err(|e| format!("下载失败: {}", e))?;
    let zip_data = response.bytes().await.map_err(|e| format!("读取响应体失败: {}", e))?;
    println!("[Updater] 下载完成，大小: {} bytes", zip_data.len());

    // --- 步骤 2: 手动校验 Hash ---
    use sha2::{Digest, Sha256};
    let mut hasher = Sha256::new();
    hasher.update(&zip_data);
    let calculated_hash = hex::encode(hasher.finalize());

    if calculated_hash.to_lowercase() != hash.to_lowercase() {
        return Err("启动器文件校验失败！下载的文件可能已损坏或被篡改。".into());
    }
    println!("[Updater] 文件校验成功。");

    // --- 步骤 3: 解压到临时目录 ---
    let temp_dir = tempfile::Builder::new()
        .prefix("launcher-update-")
        .tempdir()
        .map_err(|e| format!("创建临时目录失败: {}", e))?;

    // ✨ 核心修正 1: 创建一个临时文件来存放我们的 ZIP 数据
    let temp_zip_path = temp_dir.path().join("update.zip");
    let mut temp_zip_file = File::create(&temp_zip_path)
        .map_err(|e| format!("创建临时ZIP文件失败: {}", e))?;
    temp_zip_file.write_all(&zip_data)
        .map_err(|e| format!("写入临时ZIP文件失败: {}", e))?;

    println!("[Updater] 已将下载内容写入临时文件: {}", temp_zip_path.display());

    let bin_name = "yaesandbox-launcher.exe";

    // ✨ 核心修正 2: 现在我们从一个文件路径来解压，类型匹配了！
    self_update::Extract::from_source(&temp_zip_path)
        .archive(self_update::ArchiveKind::Zip)
        .extract_file(temp_dir.path(), bin_name)
        .map_err(|e| format!("从ZIP包解压 '{}' 失败: {}", bin_name, e))?;

    let new_exe_path = temp_dir.path().join(bin_name);
    if !new_exe_path.exists() {
        return Err(format!("解压后未在临时目录中找到 '{}'", bin_name));
    }
    println!("[Updater] 已成功解压新版本到: {}", new_exe_path.display());

    // --- 步骤 4: 创建“原子更新”标记文件 ---
    // 这是保证状态一致性的关键！
    let exe_dir = env::current_exe().map_err(|e| e.to_string())?.parent().unwrap().to_path_buf();
    let marker_path = exe_dir.join("update_pending.json");
    let marker = UpdateMarker { version: new_version };
    let marker_content = serde_json::to_string_pretty(&marker).unwrap();
    fs::write(&marker_path, marker_content).map_err(|e| format!("创建更新标记文件失败: {}", e))?;
    println!("[Updater] 已创建更新标记于: {}", marker_path.display());

    // --- 步骤 5: 调用 self_replace 执行魔法 ---
    // 这是最关键的一步。这个函数会处理所有平台相关的棘手问题。
    println!("[Updater] 准备执行自我替换...");
    self_update::self_replace::self_replace(&new_exe_path)
        .map_err(|e| format!("自我替换操作失败: {}", e))?;

    // 如果 self_replace 成功，当前进程已经被替换，但仍在运行旧代码。
    // 我们需要重启来加载新版本的代码。
    println!("[Updater] 替换成功！准备重启应用...");
    app_handle.restart();

    #[allow(unreachable_code)]
    Ok(())
}