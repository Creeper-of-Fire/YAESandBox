// src-tauri/src/commands/updater_cmd.rs
use serde::{Deserialize, Serialize};
use std::env;
use std::fs;
use std::io::{Cursor, Read, Write};
use std::path::Path;
use std::process::Command as StdCommand;
use tauri::{command, AppHandle, Manager};

// 与 launcher-version.json 匹配的结构体
#[derive(Deserialize, Serialize, Debug, Clone)]
pub struct VersionManifest {
    version: String,
    url: String,
    hash: String,
}

/// 检查启动器自身更新的状态
#[derive(Serialize, Clone)]
#[serde(tag = "tag", content = "content")] // Makes it easier to handle in JS/TS
pub enum UpdateStatus {
    UpToDate,                         // 已是最新
    UpdateAvailable(VersionManifest), // 发现新版本
}

/// 检查启动器是否有新版本
#[command]
pub async fn check_launcher_update(
    manifest_url: String,
    proxy: Option<String>,
) -> Result<UpdateStatus, String> {
    // 1. 获取当前应用版本
    let current_version_str = env!("CARGO_PKG_VERSION");
    let current_version = semver::Version::parse(current_version_str)
        .map_err(|e| format!("无效的当前版本号: {}", e))?;

    println!("[Updater] 当前版本: {}", current_version);

    // 2. 下载清单文件
    let client = crate::core::http::create_http_client(proxy.as_deref())?;
    let manifest_text = client
        .get(&manifest_url)
        .send()
        .await
        .map_err(|e| format!("下载更新清单失败: {}", e))?
        .text()
        .await
        .map_err(|e| format!("读取清单内容失败: {}", e))?;

    let manifest: VersionManifest =
        serde_json::from_str(&manifest_text).map_err(|e| format!("解析清单JSON失败: {}", e))?;

    // 3. 比较版本
    let latest_version = semver::Version::parse(&manifest.version)
        .map_err(|e| format!("无效的清单版本号: {}", e))?;

    println!("[Updater] 最新版本: {}", latest_version);

    if latest_version > current_version {
        println!("[Updater] 发现新版本！");
        Ok(UpdateStatus::UpdateAvailable(manifest))
    } else {
        println!("[Updater] 启动器已是最新版本。");
        Ok(UpdateStatus::UpToDate)
    }
}

/// 执行更新流程
#[command]
pub async fn apply_launcher_update(
    app_handle: AppHandle,
    manifest: VersionManifest,
    proxy: Option<String>,
) -> Result<(), String> {
    // 步骤 1 & 2: 下载和校验 (跨平台通用)
    println!("[Updater] 正在下载新版本: {}", manifest.url);

    let client = crate::core::http::create_http_client(proxy.as_deref())?;

    let response = client
        .get(&manifest.url)
        .send()
        .await
        .map_err(|e| format!("下载新版本失败: {}", e))?;

    let zip_data = response
        .bytes()
        .await
        .map_err(|e| format!("读取响应体失败: {}", e))?;

    use sha2::{Digest, Sha256};
    let mut hasher = Sha256::new();
    hasher.update(&zip_data);
    let calculated_hash = hex::encode(hasher.finalize());

    if calculated_hash.to_lowercase() != manifest.hash.to_lowercase() {
        return Err("文件校验失败！下载的文件可能已损坏或被篡改。".into());
    }
    println!("[Updater] 文件校验成功。");

    // 步骤 3 & 4: 解压和准备路径 (跨平台通用)
    let current_exe = env::current_exe().expect("无法获取当前执行文件路径");
    let exe_dir = current_exe.parent().expect("无法获取父目录");

    let temp_dir = exe_dir.join("update_temp");
    if temp_dir.exists() {
        fs::remove_dir_all(&temp_dir).ok();
    }
    fs::create_dir_all(&temp_dir).map_err(|e| format!("创建临时目录失败: {}", e))?;

    println!("[Updater] 正在解压到临时目录: {}", temp_dir.display());
    zip::ZipArchive::new(Cursor::new(zip_data))
        .map_err(|e| format!("解压失败: {}", e))?
        .extract(&temp_dir)
        .map_err(|e| format!("解压到临时目录失败: {}", e))?;

    // 步骤 5: 调用平台特定的替换逻辑
    #[cfg(windows)]
    prepare_and_execute_update_windows(&current_exe, &temp_dir)?;

    #[cfg(any(target_os = "macos", target_os = "linux"))]
    prepare_and_execute_update_unix(&current_exe, &temp_dir)?;

    // 步骤 6: 退出当前应用 (跨平台通用)
    println!("[Updater] 准备重启以应用更新...");
    app_handle.exit(0);

    Ok(())
}

// --- Windows 特定逻辑 ---
#[cfg(windows)]
fn prepare_and_execute_update_windows(current_exe: &Path, temp_dir: &Path) -> Result<(), String> {
    let exe_dir = current_exe.parent().unwrap();
    let script_path = exe_dir.join("apply_update.cmd");
    // 注意：这里的 new_exe_path 需要根据你打包的 zip 结构来确定
    let new_exe_path = temp_dir.join("yaesandbox-launcher.exe");

    let script_content = format!(
        r#"
@echo off
echo 等待旧进程退出...
timeout /t 2 /nobreak > nul
echo 正在替换文件...
move /Y "{new}" "{old}"
echo 启动新版本...
start "" "{old}"
echo 清理临时文件和脚本...
rd /s /q "{temp}"
(goto) 2>nul & del "%~f0"
"#,
        new = new_exe_path.display(),
        old = current_exe.display(),
        temp = temp_dir.display()
    );

    fs::write(&script_path, script_content).map_err(|e| format!("创建更新脚本失败: {}", e))?;

    StdCommand::new("cmd")
        .arg("/C")
        .arg(script_path)
        .spawn()
        .map_err(|e| format!("启动更新脚本失败: {}", e))?;

    Ok(())
}

// --- Unix (macOS & Linux) 特定逻辑 ---
#[cfg(any(target_os = "macos", target_os = "linux"))]
fn prepare_and_execute_update_unix(current_exe: &Path, temp_dir: &Path) -> Result<(), String> {
    use std::os::unix::fs::PermissionsExt;

    let exe_dir = current_exe.parent().unwrap();
    let script_path = exe_dir.join("apply_update.sh");
    // 在 Unix 上，可执行文件通常没有 .exe 后缀
    // 如果你的 Tauri 构建配置为 macOS 生成了 .app 包，这里的逻辑会更复杂
    // 我们先假设是单个二进制文件
    let new_exe_path = temp_dir.join("yaesandbox-launcher");

    let script_content = format!(
        r#"
#!/bin/sh
echo "等待旧进程退出..."
sleep 2
echo "正在替换文件..."
mv -f "{new}" "{old}"
echo "设置执行权限..."
chmod +x "{old}"
echo "启动新版本..."
"{old}" &
echo "清理临时文件和脚本..."
rm -rf "{temp}"
rm -- "$0"
"#,
        new = new_exe_path.display(),
        old = current_exe.display(),
        temp = temp_dir.display()
    );

    fs::write(&script_path, script_content).map_err(|e| format!("创建更新脚本失败: {}", e))?;

    // 必须给 shell 脚本设置可执行权限
    fs::set_permissions(&script_path, fs::Permissions::from_mode(0o755))
        .map_err(|e| format!("设置脚本权限失败: {}", e))?;

    // 在 Unix 上，我们用 sh 来执行脚本
    StdCommand::new("sh")
        .arg(script_path)
        .spawn()
        .map_err(|e| format!("启动更新脚本失败: {}", e))?;

    Ok(())
}
