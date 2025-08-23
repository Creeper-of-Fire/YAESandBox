import { app, BrowserWindow } from 'electron';
import path from 'path';
import { spawn } from 'child_process';

// 只在打包后的应用中有效，因为开发时不运行此文件
if (!app.isPackaged) {
    console.warn("[Electron Main] This script is intended for packaged application only. Exiting in dev mode.");
    // 在开发模式下直接退出，防止意外运行
    app.quit();
    process.exit(0);
}

let backendProcess = null;
let mainWindow = null;

function startBackend() {
    return new Promise((resolve, reject) => {
        // 我们的组装脚本保证了后端可执行文件和 Electron 主程序在同一目录下
        const exeName = 'YAESandBox.AppWeb.exe';
        const backendPath = path.join(path.dirname(app.getPath('exe')), exeName);

        console.log(`[Electron] Starting backend at: ${backendPath}`);

        // 检查文件是否存在，提供更明确的错误信息
        try {
            require('fs').statSync(backendPath);
        } catch (error) {
            console.error(`[Electron] Backend executable not found at path: ${backendPath}`);
            return reject(new Error(`Backend executable not found.`));
        }

        backendProcess = spawn(backendPath);

        // 监听 STDOUT 捕获 URL
        backendProcess.stdout.on('data', (data) => {
            const output = data.toString();
            // 打印所有后端日志，便于调试
            console.log(`[Backend] ${output.trim()}`);

            const match = output.match(/Now listening on: (https?:\/\/[^\s]+)/);
            if (match && match[1]) {
                const backendUrl = match[1];
                console.log(`[Electron] Backend is ready at: ${backendUrl}`);
                resolve(backendUrl);
            }
        });

        backendProcess.stderr.on('data', (data) => {
            console.error(`[Backend ERR] ${data.toString().trim()}`);
        });

        backendProcess.on('close', (code) => {
            console.log(`[Electron] Backend process exited with code ${code}. Quitting app.`);
            // 如果后端意外退出，也应该关闭整个应用
            if (!app.isQuitting) {
                app.quit();
            }
        });

        backendProcess.on('error', (err) => {
            console.error(`[Electron] Failed to start backend process:`, err);
            reject(err);
        });
    });
}

function createWindow(url) {
    mainWindow = new BrowserWindow({
        width: 1440,
        height: 900,
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true,
            sandbox: true
        },
        // 添加一个加载中的标题
        title: 'YAESandBox - Loading...'
    });

    mainWindow.loadURL(url);

    // 加载完成后更新标题
    mainWindow.webContents.on('did-finish-load', () => {
        mainWindow.setTitle('YAESandBox');
    });

    mainWindow.on('closed', () => {
        mainWindow = null;
    });
}

// --- Electron App 生命周期 ---
app.whenReady().then(async () => {
    try {
        const url = await startBackend();
        createWindow(url);
    } catch (error) {
        console.error('[Electron] Fatal error during startup:', error);
        // 可以显示一个错误对话框给用户
        const { dialog } = require('electron');
        dialog.showErrorBox('Application Error', `Failed to start the backend service. Please check the logs.\n\n${error.message}`);
        app.quit();
    }
});

app.on('window-all-closed', () => {
    app.quit();
});

// 确保在退出时杀死后端进程
app.on('before-quit', () => {
    app.isQuitting = true;
    if (backendProcess) {
        console.log('[Electron] App is quitting, killing backend process...');
        backendProcess.kill();
        backendProcess = null;
    }
});