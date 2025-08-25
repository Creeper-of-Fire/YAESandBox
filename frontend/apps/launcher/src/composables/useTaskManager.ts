// src/composables/useTaskManager.ts

import { ref, readonly, computed } from 'vue';
import { invoke } from '@tauri-apps/api/core';
import { listen, type UnlistenFn } from '@tauri-apps/api/event';
import { useConfig } from './useConfig';

// --- 类型定义 ---
/**
 * 描述一个可执行的更新/安装任务。
 * 这是一个通用的数据结构，适用于任何组件。
 */
export interface UpdateTask {
    id: string;          // 唯一标识符
    name: string;        // 显示名称
    version: string;     // 目标版本
    url: string;         // 下载URL
    hash: string;        // SHA256 哈希
    extractPath: string; // 解压到的相对路径
}

interface DownloadProgressPayload {
    id: string;
    downloaded: number;
    total: number | null;
}

// --- Composable 全局状态 ---
const currentTask = ref<UpdateTask | null>(null);
const statusMessage = ref('任务管理器准备就_绪。');
const isBusy = ref(false);
const error = ref<string | null>(null);
const progress = ref({ percentage: 0, text: '' });

// --- Composable 实现 ---
let unlisten: UnlistenFn | null = null;
async function setupProgressListener() {
    if (unlisten) return;
    unlisten = await listen<DownloadProgressPayload>('download-progress', (event) => {
        // 只更新当前正在执行的任务的进度
        if (currentTask.value && event.payload.id === currentTask.value.id) {
            const { downloaded, total } = event.payload;
            if (total) {
                const percentage = Math.round((downloaded / total) * 100);
                const downloadedMb = (downloaded / 1024 / 1024).toFixed(2);
                const totalMb = (total / 1024 / 1024).toFixed(2);
                progress.value = {
                    percentage,
                    text: `${downloadedMb}MB / ${totalMb}MB`,
                };
            } else {
                const downloadedMb = (downloaded / 1024 / 1024).toFixed(2);
                progress.value = {
                    percentage: 0,
                    text: `已下载 ${downloadedMb}MB`,
                };
            }
        }
    });
}

/**
 * 通用的任务执行器 Composable。
 * 接收一个任务，并按顺序执行下载、校验、解压、清理。
 */
export function useTaskManager() {
    const { config } = useConfig();
    setupProgressListener();

    /**
     * 执行单个更新任务。
     * @param task 要执行的任务对象。
     * @returns {Promise<boolean>} 成功返回 true，失败返回 false。
     */
    const executeTask = async (task: UpdateTask): Promise<boolean> => {
        if (isBusy.value) {
            statusMessage.value = '错误：另一个任务正在进行中。';
            return false;
        }

        // --- 状态初始化 ---
        isBusy.value = true;
        currentTask.value = task;
        error.value = null;
        progress.value = { percentage: 0, text: '' };

        try {
            // 步骤 1: 下载并校验
            statusMessage.value = `正在下载 ${task.name}...`;
            const savePath = `downloads/${task.id}.zip`;
            await invoke('download_and_verify_zip', {
                id: task.id,
                url: task.url,
                relativePath: savePath,
                expectedHash: task.hash,
                proxy: config.value?.proxy_address,
            });

            // 步骤 2: 解压
            statusMessage.value = `正在安装 ${task.name}...`;
            await invoke('unzip_file', {
                zipRelativePath: savePath,
                targetRelativeDir: task.extractPath,
            });

            // 步骤 3: 更新本地版本记录
            await invoke('update_local_version', {
                componentId: task.id,
                newVersion: task.version,
            });

            // 步骤 4: 清理
            await invoke("delete_file", { relativePath: savePath });


            statusMessage.value = `${task.name} (v${task.version}) 已成功安装！`;
            return true;
        } catch (e) {
            const errorMessage = `任务 [${task.name}] 失败: ${String(e)}`;
            statusMessage.value = errorMessage;
            error.value = errorMessage;
            console.error(errorMessage, e);
            return false;
        } finally {
            // --- 状态重置 ---
            isBusy.value = false;
            currentTask.value = null;
        }
    };

    return {
        currentTask: readonly(currentTask),
        statusMessage: readonly(statusMessage),
        isBusy: readonly(isBusy),
        error: readonly(error),
        progress: readonly(progress),
        executeTask,
    };
}