// launcher/src/log_viewer.ts
import { invoke } from '@tauri-apps/api/core';
// @ts-ignore
import './styles.css';

async function loadLogs() {
    const logContentElement = document.getElementById('log-content');
    if (!logContentElement) return;

    try {
        const logs = await invoke<string>('get_log_content');
        logContentElement.textContent = logs;
        // 自动滚动到底部
        logContentElement.scrollTop = logContentElement.scrollHeight;
    } catch (error) {
        logContentElement.textContent = `加载日志失败: ${error}`;
    }
}

// 确保 DOM 加载完毕后再执行
document.addEventListener('DOMContentLoaded', () => {
    loadLogs().then(_ => {});
});