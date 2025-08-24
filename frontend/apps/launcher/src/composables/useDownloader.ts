// src/composables/useDownloader.ts

/**
 * @file useDownloader.ts
 * @module composables
 * @description 提供了文件下载、解压和清理功能的 Vue Composition API 可组合函数。
 *              专为 Tauri 应用设计，通过调用 Rust 后端服务实现文件操作，
 *              并提供下载进度反馈。
 */

import {computed, onMounted, onUnmounted, type Ref, ref} from 'vue';
import {invoke} from '@tauri-apps/api/core';
import {listen, type UnlistenFn} from '@tauri-apps/api/event';

/**
 * @interface DownloadableItem
 * @description 定义一个可下载/更新项的数据结构。
 */
export interface DownloadableItem {
    /**
     * @property {string} id - 唯一标识符，用于跟踪当前正在下载的项目。
     */
    id: string;
    /**
     * @property {string} name - 文件的显示名称，用于用户界面提示。例如："我的应用程序"。
     */
    name: string;
    /**
     * @property {string} url - 待下载文件的完整URL。
     */
    url: string;
    /**
     * @property {string} savePath - 文件下载后在应用数据目录中的相对路径。
     *                                 例如："downloads/app.zip"
     */
    savePath: string;
    /**
     * @property {string} extractPath - ZIP文件解压后，内容存放的相对目录。
     *                                   例如："app" 会将 app.zip 的内容解压到 app 目录。
     */
    extractPath: string;
}

/**
 * @function sleep
 * @description 创建一个异步延迟。
 *              因为Rust太™的快了，所以为了用户体验，我们加入了一个微小的延迟，以免用户懵逼。
 * @param {number} ms - 延迟的毫秒数。
 * @returns {Promise<void>}
 */
const sleep = (ms: number): Promise<unknown> => new Promise(resolve => setTimeout(resolve, ms));

/**
 * @function useDownloader
 * @description 提供下载、解压和清理文件功能的 Composition API 可组合函数。
 *              暴露下载状态、进度和触发更新的方法。
 */
export function useDownloader() {
    // --- 1. State (状态管理) ---
    /**
     * 存储当前操作的状态消息，用于向用户显示。例如："正在下载...", "下载完成。", "更新失败: ..."
     */
    const statusMessage: Ref<string, string> = ref('启动器已准备就绪。');

    /**
     * 布尔值，指示当前是否有文件正在下载或处理。
     */
    const isDownloading: Ref<boolean, boolean> = ref(false);

    /**
     * 存储当前正在处理的 DownloadableItem 的 ID。如果没有正在处理的项目，则为 null。
     */
    const currentlyDownloadingId: Ref<string | null, string | null> = ref<string | null>(null);

    /**
     * 存储实时的下载进度信息。`downloaded` 是已下载的字节数，`total` 是文件的总字节数。
     */
    const downloadProgress = ref({downloaded: 0, total: 0});

    /**
     * Tauri 事件监听器的取消函数。用于在组件卸载时清理事件监听器，防止内存泄漏。
     */
    let unlisten: UnlistenFn | null = null;

    // --- 2. Computed (计算属性) ---
    /**
     * 根据 `downloadProgress` 计算出的下载百分比 (0-100)。
     * 如果 `total` 为 0，则返回 0。
     */
    const progressPercentage = computed(() => {
        if (!downloadProgress.value.total) return 0;
        return Math.round((downloadProgress.value.downloaded / downloadProgress.value.total) * 100);
    });

    /**
     * 格式化后的下载进度文本，包含百分比和已下载/总大小。
     * 例如："50% - 10.5 MB / 21.0 MB" 或 "10.5 MB 已下载" (如果总大小未知)。
     */
    const progressText = computed(() => {
        if (!isDownloading.value) return '';
        if (downloadProgress.value.total) {
            return `${progressPercentage.value}% - ${formatBytes(downloadProgress.value.downloaded)} / ${formatBytes(downloadProgress.value.total)}`;
        }
        return `${formatBytes(downloadProgress.value.downloaded)} 已下载`;
    });

    // --- 3. Method (方法) ---
    /**
     * @function performUpdate
     * @description 执行一个完整的更新/安装流程：下载、解压、清理。
     *              这个函数会更新内部状态以反映当前操作。
     * @param {DownloadableItem} item - 包含要下载和处理的文件的详细信息。
     * @returns {Promise<void>} - 当所有操作完成（成功或失败）时解析。
     * @async
     */
    async function performUpdate(item: DownloadableItem): Promise<void> {
        isDownloading.value = true;
        currentlyDownloadingId.value = item.id;
        downloadProgress.value = {downloaded: 0, total: 0};

        try {
            // 步骤 1: 下载
            statusMessage.value = `正在下载 ${item.name}...`;
            await invoke("download_file", {
                url: item.url,
                relativePath: item.savePath,
            });

            // 步骤 2: 解压
            statusMessage.value = `下载完成。正在解压 ${item.name}...`;
            // 在调用 Rust 之前，先让 UI 渲染一下
            await sleep(50);
            await invoke("unzip_file", {
                zipRelativePath: item.savePath,
                targetRelativeDir: item.extractPath,
            });

            // 稍作延迟
            await sleep(300);

            // 步骤 3: 清理下载的 .zip 文件 (只在成功后执行)
            statusMessage.value = `正在清理安装文件...`;
            await invoke("delete_file", {
                relativePath: item.savePath,
            });

            // 稍作延迟
            await sleep(300);

            statusMessage.value = `${item.name} 已成功安装/更新！`;
        } catch (error) {
            console.error("更新失败:", error);
            statusMessage.value = `更新失败: ${String(error)}`;
        } finally {
            // 无论成功或失败，最后都重置下载状态
            isDownloading.value = false;
            currentlyDownloadingId.value = null;
        }
    }

    // --- 4. Lifecycle (生命周期) ---
    /**
     * @hook onMounted
     * @description 在组件挂载后执行的钩子函数。
     *              在此处注册一个 Tauri 事件监听器，用于接收后端发送的下载进度更新事件。
     *              事件数据将用于更新 `downloadProgress` 状态。
     */
    onMounted(async () => {
        unlisten = await listen<{ downloaded: number; total: number | null }>(
            'download-progress',
            (event) => {
                downloadProgress.value = {
                    downloaded: event.payload.downloaded,
                    total: event.payload.total || 0,
                };
            }
        );
    });

    /**
     * @hook onUnmounted
     * @description 在组件卸载前执行的钩子函数。
     *              在此处调用 `unlisten` 函数，取消之前注册的事件监听器，
     *              以避免内存泄漏和不必要的事件处理。
     */
    onUnmounted(() => {
        if (unlisten) unlisten();
    });

    // --- 5. 返回所有需要暴露给组件的变量和方法 ---
    return {
        statusMessage,
        isDownloading,
        currentlyDownloadingId,
        progressPercentage,
        progressText,
        performUpdate,
    };
}

// --- 辅助函数 ---
/**
 * @function formatBytes
 * @description 将字节数格式化为人类可读的字符串（例如："1024 字节", "1.50 MB"）。
 * @param bytes - 待格式化的字节数。
 * @param [decimals=2] - 小数位数，默认为 2。
 * @returns {string} 格式化后的字符串。
 */
function formatBytes(bytes: number, decimals = 2): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}