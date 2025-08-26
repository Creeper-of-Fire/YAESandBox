// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use native_dialog::{MessageDialogBuilder, MessageLevel};
use serde::Deserialize;
use std::env;
use std::fs;
use std::panic;

/// 程序的入口点。
/// 检查命令行参数以确定是正常启动还是执行更新任务。
fn main() {
    // --- 设置全局 Panic Hook ---
    // 这必须在任何可能 panic 的代码之前设置
    panic::set_hook(Box::new(|panic_info| {
        // panic_info 包含了 panic 的位置和原因
        let payload = panic_info
            .payload()
            .downcast_ref::<&str>()
            .unwrap_or(&"未知错误");
        let location = panic_info.location().map_or("未知位置".to_string(), |loc| {
            format!("{}:{}:{}", loc.file(), loc.line(), loc.column())
        });

        let error_message = format!(
            "发生了一个无法恢复的严重错误，应用即将退出。\n\n\
            错误详情:\n{}\n\n\
            位置:\n{}",
            payload, location
        );

        // 记录到控制台，方便调试
        eprintln!("{}", error_message);

        // 弹出原生对话框
        MessageDialogBuilder::default()
            .set_title("应用程序严重错误")
            .set_text(&error_message)
            .set_level(MessageLevel::Error)
            .alert() // show_alert 阻塞直到用户点击 "OK"
            .show()
            .unwrap_or_else(|e| eprintln!("无法显示错误对话框: {}", e));

        // 注意：钩子返回后，线程仍然会终止。
        // 如果是主线程 panic，整个程序会退出。
    }));

    // 在启动Tauri之前，先执行更新的收尾工作
    if let Err(e) = finalize_pending_update() {
        // 即使收尾失败，我们也不应该阻止应用启动
        eprintln!("[Main] 完成更新时发生错误 (不影响启动): {}", e);
    }

    // 正常启动您的Tauri应用
    yaesandbox_launcher_lib::run();
}

#[derive(Deserialize)]
struct UpdateMarker {
    version: String,
}

/// 检查并完成待处理的更新（如果有）。
/// 这个函数应该在Tauri应用启动前被调用。
fn finalize_pending_update() -> Result<(), String> {
    let exe_dir = env::current_exe()
        .map_err(|e| e.to_string())?
        .parent()
        .unwrap()
        .to_path_buf();
    let marker_path = exe_dir.join("update_pending.json");

    if marker_path.exists() {
        println!("[Main] 检测到待处理的更新标记文件...");
        let content =
            fs::read_to_string(&marker_path).map_err(|e| format!("读取标记文件失败: {}", e))?;
        let marker: UpdateMarker =
            serde_json::from_str(&content).map_err(|e| format!("解析标记文件失败: {}", e))?;

        // 在这里，直接调用我们已经存在的、健壮的 update_local_version 命令！
        // 这需要一个 AppHandle，所以最好在 tauri::Builder 的 setup 钩子中完成。
        // 或者，为了简单起见，我们直接在这里修改 local_versions.json。

        let versions_path = exe_dir.join("local_versions.json");
        let mut versions: std::collections::HashMap<String, String> = if versions_path.exists() {
            let v_content = fs::read_to_string(&versions_path).map_err(|e| e.to_string())?;
            serde_json::from_str(&v_content).map_err(|e| e.to_string())?
        } else {
            std::collections::HashMap::new()
        };

        versions.insert("launcher".to_string(), marker.version.clone());

        let new_v_content = serde_json::to_string_pretty(&versions).map_err(|e| e.to_string())?;
        fs::write(versions_path, new_v_content).map_err(|e| e.to_string())?;
        println!(
            "[Main] local_versions.json 已更新至版本: {}",
            marker.version
        );

        // 最关键的一步：删除标记文件，防止下次启动时重复执行
        fs::remove_file(marker_path).map_err(|e| format!("删除标记文件失败: {}", e))?;
        println!("[Main] 更新标记文件已删除。更新流程彻底完成。");
    }

    Ok(())
}
