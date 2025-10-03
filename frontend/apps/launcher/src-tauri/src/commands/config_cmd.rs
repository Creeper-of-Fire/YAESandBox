// src-tauri/src/commands/config_cmd.rs
use std::path::Path;
use crate::core::config;
use crate::core::config::ConfigEntry;
use crate::AppState;
use dark_light::Mode;
use tauri::{command, AppHandle, Emitter, Manager, State, Theme};

/// 读取、初始化并返回扁平化的配置条目列表。
#[command]
pub fn read_config(app_state: State<'_, AppState>) -> Result<Vec<ConfigEntry>, String> {
    config::get_config_as_entries(&app_state.app_dir)
}

/// 从前端接收配置条目列表，保存并应用更改。
#[command]
pub fn write_config(
    app_handle: AppHandle,
    app_state: State<'_, AppState>,
    entries: Vec<ConfigEntry>,
) -> Result<(), String> {
    // --- 找到 payload 中的新主题 ---
    let new_theme = entries
        .iter()
        .find(|&e| e.section == "Appearance" && e.key == "theme")
        .map(|e| e.value.clone());

    match new_theme {
        Some(theme) => {
            log::info!("[Config] 配置已更新，正在应用主题 '{}'...", theme);
            set_and_apply_theme(&app_state.app_dir, &app_handle, &theme)?;
        }
        None => {
            log::info!("[Config] 配置已更新，但未找到主题。");
        }
    }

    // --- 总是保存所有配置更改 ---
    config::set_config_from_entries(&app_state.app_dir, &entries)?;
    log::info!("[Config] 前端提交的配置已保存。");

    // --- 总是触发事件，因为其他配置项可能已更改 ---
    emit_config_changed(&app_handle);

    Ok(())
}

#[cfg(windows)]
use windows::{
    core::PCWSTR,
    Win32::Foundation::HWND,
    Win32::UI::WindowsAndMessaging::{MessageBoxW, IDYES, MB_APPLMODAL, MB_ICONINFORMATION, MB_YESNO},
};

/// [Windows Only] 使用原生 Win32 API 显示一个真正的、阻塞的、模态对话框。
/// 这个函数会阻塞调用它的线程，直到用户做出选择。
#[cfg(windows)]
pub(crate) fn show_modal_restart_prompt(app_handle: AppHandle) {
    // 关键：在单独的线程中运行，以避免阻塞 Tauri 的主异步运行时
    std::thread::spawn(move || {
        let main_window = match app_handle.get_webview_window("main") {
            Some(window) => window,
            None => {
                log::error!("[Win32 Dialog] 无法获取主窗口句柄，无法显示重启提示。");
                return;
            }
        };

        // 1. 获取父窗口的 HWND
        // `hwnd()` 返回 Result，我们假设窗口存在时句柄一定有效
        let parent_hwnd_from_tauri = match main_window.hwnd() {
            Ok(hwnd) => hwnd,
            Err(e) => {
                log::error!("[Win32 Dialog] 获取 HWND 失败: {}，无法显示重启提示。", e);
                return;
            }
        };

        let parent_hwnd = HWND(parent_hwnd_from_tauri.0);

        // 2. 准备 Win32 API 需要的宽字符（UTF-16）字符串
        let title = "主题设置提示\0".encode_utf16().collect::<Vec<u16>>();
        let message = "主题已更新。在 Windows 系统上，需要重启应用才能完全应用新主题。\n\n您想现在重启吗？\0".encode_utf16().collect::<Vec<u16>>();

        // 3. 调用 MessageBoxW
        //    这是一个阻塞调用，会暂停这个线程的执行
        let result = unsafe {
            MessageBoxW(
                Some(parent_hwnd),
                PCWSTR(message.as_ptr()),
                PCWSTR(title.as_ptr()),
                MB_YESNO | MB_ICONINFORMATION | MB_APPLMODAL,
            )
        };

        // 4. 根据用户的选择执行操作
        if result == IDYES {
            log::info!("[Win32 Dialog] 用户选择立即重启以应用主题。");
            app_handle.restart();
        } else {
            log::info!("[Win32 Dialog] 用户选择稍后重启。");
        }
    });
}

/// 一个辅助函数，用于显示平台特定的主题更改通知。
#[cfg(windows)]
fn show_theme_change_notification(app_handle: &AppHandle) {
    // 直接调用我们新的、绝对可靠的对话框函数
    let app_handle = app_handle.clone();
    show_modal_restart_prompt(app_handle);
}

/// 将主题设置写入配置文件，并立即在窗口上应用。
/// 这是菜单事件的逻辑。
pub fn set_and_apply_theme(
    app_dir: &Path,
    app_handle: &AppHandle,
    theme_to_set: &str,
) -> Result<(), String> {
    // --- 1. 获取当前主题，避免不必要的操作 ---
    let doc = config::load_or_initialize(app_dir)?;
    let current_theme = config::get_value(&doc, "Appearance", "theme")
        .unwrap_or_else(|| "auto".to_string());

    // --- 2. 仅当主题实际发生变化时才继续 ---
    if current_theme != theme_to_set {
        log::info!("[Config] 主题从 '{}' 变更为 '{}'，正在应用...", current_theme, theme_to_set);
        // 写入配置
        config::set_value(app_dir, "Appearance", "theme", theme_to_set)?;
        // 应用主题
        apply_theme(app_handle, theme_to_set)?;

        // 在 Windows 上显示提示
        #[cfg(windows)]
        show_theme_change_notification(app_handle);

        // 通知前端配置已更改
        emit_config_changed(app_handle);
    } else {
        log::info!("[Config] 选择的主题 '{}' 已是当前主题，无需更改。", theme_to_set);
    }
    Ok(())
}


fn emit_config_changed(app_handle: &AppHandle) {
    app_handle.emit("config-changed", ()).unwrap();
}

/// 根据给定的主题字符串，返回匹配的 Tauri 主题枚举。
pub fn get_theme_from_string(theme: &str) -> Theme {
    match theme {
        "light" => Theme::Light,
        "dark" => Theme::Dark,
        "auto" | _ => {
            // 使用 dark-light 库来检测系统主题
            match dark_light::detect() {
                Ok(Mode::Dark) => Theme::Dark,
                Ok(Mode::Light) => Theme::Light,
                _ => Theme::Light,
            }
        }
    }
}

/// 根据给定的主题字符串，实际更改 Tauri 窗口的主题。
pub fn apply_theme(app_handle: &AppHandle, theme: &str) -> Result<(), String> {
    let theme_to_set = get_theme_from_string(theme);
    app_handle.set_theme(Option::from(theme_to_set));
    Ok(())
}