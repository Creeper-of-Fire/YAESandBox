// src/stores/persistenceStore.ts
import {defineStore} from 'pinia';
import {ref} from 'vue';
import {PersistenceService} from '@/types/generated/api'; // 引入 API Service
import {useTopologyStore} from './topologyStore.ts';
import {useBlockContentStore} from './blockContentStore.ts';
import {useBlockStatusStore} from './blockStatusStore.ts';
import {useConnectionStore} from '../../stores/connectionStore.ts'; // 用于检查 SignalR 状态

// 定义盲存数据的结构 (前端关心的数据)
interface BlindData
{
    pathSelection?: Record<string, string>;
    currentPathLeafId?: string | null;
    // 未来可以添加其他需要前端保存的状态，如 UI 布局、面板状态等
    // uiState?: { leftOpen: boolean, rightOpen: boolean, ... };
}

export const usePersistenceStore = defineStore('persistence', () =>
{
    // --- State ---
    const isSaving = ref(false);
    const isLoading = ref(false);
    const lastError = ref<string | null>(null);

    // --- Getters ---
    // 可以添加一些计算属性，比如 lastSaveTime 等，如果需要的话

    // --- Actions ---
    const topologyStore = useTopologyStore();
    const blockContentStore = useBlockContentStore();
    const blockStatusStore = useBlockStatusStore();
    const connectionStore = useConnectionStore();

    /**
     * 保存当前会话状态到文件。
     */
    async function saveSession()
    {
        if (isSaving.value) return;
        isSaving.value = true;
        lastError.value = null;
        console.log("PersistenceStore: 开始保存会话...");

        // 1. 准备盲存数据
        const blindData: BlindData = {
            pathSelection: topologyStore.pathSelection,
            currentPathLeafId: topologyStore.currentPathLeafId,
            // Add other frontend state if needed
        };

        try
        {
            // 2. 调用 API 保存
            const blob = await PersistenceService.postApiPersistenceSave({requestBody: blindData});

            // 3. 创建下载链接并触发下载
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            // 生成文件名，例如：yaesandbox_session_2023-10-27_10-30-00.json
            const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
            a.download = `yaesandbox_session_${timestamp}.json`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            window.URL.revokeObjectURL(url);

            console.log("PersistenceStore: 会话保存成功。");
            // 可以在这里显示成功提示
            // showSuccessMessage("会话已成功保存！");

        } catch (error: any)
        {
            console.error("PersistenceStore: 保存会话失败", error);
            lastError.value = `保存失败: ${error.message || error}`;
            // 显示错误提示
            alert(`保存失败: ${error.message || error}`);
        } finally
        {
            isSaving.value = false;
        }
    }

    /**
     * 从文件加载会话状态。
     * @param archiveFile - 用户选择的 JSON 存档文件。
     */
    async function loadSession(archiveFile: File)
    {
        if (isLoading.value) return;
        isLoading.value = true;
        lastError.value = null;
        console.log("PersistenceStore: 开始加载会话...");

        // --- 准备工作 ---
        // 0. (可选) 停止 SignalR 连接，防止加载过程中收到干扰信息？
        //    或者让 BlockStatusStore 在加载期间忽略某些更新？
        //    暂时先不停止，依赖后续的状态重置
        const wasConnected = connectionStore.isConnected;
        if (wasConnected)
        {
            console.warn("PersistenceStore: 正在加载存档，SignalR 仍连接，可能会收到旧消息。");
            // await connectionStore.disconnectSignalR(); // 考虑是否需要断开
        }

        // 1. 清空所有相关 Store 的状态
        console.log("PersistenceStore: 清空当前状态...");
        topologyStore.clearTopologyState();
        blockContentStore.clearAllBlocks();
        blockStatusStore.clearAllStatuses();
        // 清空 UI Store? (如果 UI 状态也保存在盲存中)
        // uiStore.resetState();

        // 显示全局加载状态
        blockStatusStore.setLoadingAction(true, '正在加载存档...');

        try
        {
            // 2. 调用 API 加载
            const responseData = await PersistenceService.postApiPersistenceLoad({
                formData: {archiveFile: archiveFile}
            });

            // 3. 解析盲存数据
            let loadedBlindData: BlindData = {};
            if (responseData)
            {
                // responseData 可能是任何类型，需要安全地解析
                if (typeof responseData === 'object')
                {
                    loadedBlindData = responseData as BlindData;
                    console.log("PersistenceStore: 已加载盲存数据", loadedBlindData);
                } else
                {
                    console.warn("PersistenceStore: 存档中包含非对象格式的盲存数据，已忽略。", responseData);
                }
            } else
            {
                console.log("PersistenceStore: 存档中不包含盲存数据。");
            }

            // --- 加载后处理 ---
            console.log("PersistenceStore: 存档加载成功，开始重建前端状态...");

            // 4. (最重要) 触发一次完整的拓扑获取，这将重建内存图
            await topologyStore.fetchAndUpdateTopology();

            // 5. 使用盲存数据恢复路径状态 (fetchAndUpdateTopology 会内部验证，但这里显式恢复)
            //    注意：要确保拓扑获取完成后再恢复路径
            topologyStore.restorePathState({
                pathSelection: loadedBlindData.pathSelection,
                currentPathLeafId: loadedBlindData.currentPathLeafId
            });

            // 6. (可选) 触发加载当前路径上 Block 的内容 (fetchAndUpdateTopology 可能已部分处理)
            console.log("PersistenceStore: 触发加载当前路径 Block 内容...");
            const pathIds = topologyStore.getCurrentPathNodes.map(n => n.id);
            const fetchPromises = pathIds.map(id => blockContentStore.fetchAllBlockDetails(id));
            await Promise.allSettled(fetchPromises); // 等待所有内容获取尝试完成

            console.log("PersistenceStore: 会话加载完成。");
            // 显示成功提示
            // showSuccessMessage("会话已成功加载！");

        } catch (error: any)
        {
            console.error("PersistenceStore: 加载会话失败", error);
            lastError.value = `加载失败: ${error.message || error}`;
            // 显示错误提示
            alert(`加载失败: ${error.message || error}`);
            // 加载失败后，应用状态是空的，可能需要提示用户刷新或重新开始

        } finally
        {
            isLoading.value = false;
            blockStatusStore.setLoadingAction(false); // 关闭全局加载状态
            // 7. (可选) 如果之前断开了 SignalR，重新连接
            // if (wasConnected) {
            //    console.log("PersistenceStore: 尝试重新连接 SignalR...");
            //    await connectionStore.connectSignalR();
            // }
        }
    }

    return {
        isSaving,
        isLoading,
        lastError,
        saveSession,
        loadSession,
    };
});