use native_dialog::{MessageDialogBuilder, MessageLevel};
use std::process::exit;

pub(crate) fn show_critical_error_and_exit(title: &str, text: &str) {
    MessageDialogBuilder::default()
        .set_title(title)
        .set_text(text)
        .set_level(MessageLevel::Error)
        .confirm()
        .show()
        .unwrap_or(false); // 如果对话框创建失败，就当用户“确认”了

    // 无论用户是否点击了“确定”，我们都退出程序。
    // 对话框是阻塞的，所以代码会在这里暂停直到用户交互。
    println!("[Launcher] 显示严重错误并退出: {}", text);
    exit(1); // 以非零状态码退出，表示发生了错误
}