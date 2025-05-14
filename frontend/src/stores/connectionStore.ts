// src/stores/connectionStore.ts
import {defineStore} from 'pinia';
import {ref} from 'vue';
import {signalrService} from '@/services/signalrService'; // 导入 signalrService 来调用 start/stop
import {OpenAPI} from '@/types/generated/api'; // 用于获取 BASE URL

// 引入其他需要触发初始化的 Store
import {useTopologyStore} from './topologyStore';
import {useBlockContentStore} from './blockContentStore';
import {useBlockStatusStore} from './blockStatusStore';

export const useConnectionStore = defineStore('connection', () => {
    const isConnected = ref(false);
    const isConnecting = ref(false);
    const connectionError = ref<string | null>(null);

    // 引用其他 Store (在 action 中使用)
    let topologyStore: ReturnType<typeof useTopologyStore> | null = null;
    let blockContentStore: ReturnType<typeof useBlockContentStore> | null = null;
    let blockStatusStore: ReturnType<typeof useBlockStatusStore> | null = null;

    // 辅助函数确保 Store 已初始化 (Pinia 要求在组件 setup 或 action 中调用 useStore)
    const ensureStores = () => {
        if (!topologyStore) topologyStore = useTopologyStore();
        if (!blockContentStore) blockContentStore = useBlockContentStore();
        if (!blockStatusStore) blockStatusStore = useBlockStatusStore();
    }

    /**
     * 供 SignalR Service 回调或其他地方更新状态
     */
    function setSignalRConnectionStatus(connected: boolean, connecting: boolean, error: string | null = null) {
        const oldIsConnected = isConnected.value; // 记录旧状态

        isConnected.value = connected;
        isConnecting.value = connecting;
        connectionError.value = error;

        // --- 关键：监听连接成功的状态变化 ---
        // 如果是从“未连接”变为“已连接”状态
        if (!oldIsConnected && connected) {
            console.log("ConnectionStore: 检测到 SignalR 连接成功，触发应用初始化...");
            // 调用初始化应用数据的 action
            initializeAppData();
        } else if (oldIsConnected && !connected) {
            console.log("ConnectionStore: 检测到 SignalR 连接断开。");
            // 可以考虑在断开时清理部分状态，或提示用户
        }
    }

    /**
     * 连接到 SignalR Hub
     */
    async function connectSignalR() {
        // 确保不会重复连接
        if (isConnected.value || isConnecting.value) {
            console.log("ConnectionStore: 已连接或正在连接中，跳过。");
            return;
        }

        connectionError.value = null; // 清除旧错误
        ensureStores(); // 确保 Store 引用存在
        // setSignalRConnectionStatus(false, true); // 状态设置移到 signalrService 内部回调

        try {
            // 调用 signalrService 启动连接，baseUrl 从 OpenAPI 配置获取
            await signalrService.start(OpenAPI.BASE || 'http://localhost:7018'); // 提供默认值
            // 成功连接的状态更新由 signalrService 的 onreconnected 或 start 成功回调触发 setSignalRConnectionStatus
        } catch (error: any) {
            console.error("ConnectionStore: connectSignalR 失败", error);
            // 失败的状态更新由 signalrService 的 start 失败或 onclose 回调触发 setSignalRConnectionStatus
            connectionError.value = error.message || '连接失败'; // 记录错误信息
            // 这里可以不用手动调用 setSignalRConnectionStatus，依赖 service 的回调
        }
    }

    /**
     * 断开 SignalR 连接
     */
    async function disconnectSignalR() {
        if (!isConnected.value && !isConnecting.value) {
            console.log("ConnectionStore: SignalR 未连接，无需断开。");
            return;
        }
        try {
            await signalrService.stop();
            // 状态更新由 signalrService 的 stop 或 onclose 回调触发 setSignalRConnectionStatus
        } catch (error) {
            console.error("ConnectionStore: disconnectSignalR 失败", error);
            // 状态更新依赖回调
        }
    }

    /**
     * 初始化应用核心数据 (在连接成功后调用)
     */
    async function initializeAppData() {
        ensureStores(); // 再次确保 Store 实例存在
        // 使用断言确保 store 存在 (或者添加更严格的错误处理)
        if (!topologyStore || !blockContentStore || !blockStatusStore) {
            console.error("ConnectionStore: 初始化应用数据失败，依赖的 Store 不可用！");
            // 可能需要设置一个全局错误状态
            return;
        }

        console.log("ConnectionStore: 开始执行 initializeAppData...");
        blockStatusStore.setLoadingAction(true, '正在加载初始数据...'); // 设置加载状态

        try {
            // 1. 获取拓扑
            console.log("ConnectionStore: 获取初始拓扑...");
            await topologyStore.fetchAndUpdateTopology();
            console.log("ConnectionStore: 初始拓扑获取完成。");

            // 2. 获取根节点和当前路径叶节点的内容
            const fetches = [];
            if (topologyStore.rootNode?.id) {
                fetches.push(blockContentStore.fetchAllBlockDetails(topologyStore.rootNode.id));
            }
            if (topologyStore.currentPathLeafId && topologyStore.currentPathLeafId !== topologyStore.rootNode?.id) {
                fetches.push(blockContentStore.fetchAllBlockDetails(topologyStore.currentPathLeafId));
            }
            if (fetches.length > 0) {
                console.log("ConnectionStore: 获取初始 Block 内容...");
                await Promise.allSettled(fetches);
                console.log("ConnectionStore: 初始 Block 内容获取尝试完成。");
            }

            console.log("ConnectionStore: 应用数据初始化完成。");

        } catch (error) {
            console.error("ConnectionStore: initializeAppData 失败", error);
            // 显示错误提示
            blockStatusStore.setLoadingAction(false); // 确保取消加载状态
            // alert(`加载初始数据失败: ${error instanceof Error ? error.message : '未知错误'}`);
            // 可能需要通知用户刷新或重试
        } finally {
            blockStatusStore.setLoadingAction(false); // 确保取消加载状态
        }
    }


    return {
        isConnected,
        isConnecting,
        connectionError,
        setSignalRConnectionStatus, // 暴露给 signalrService 使用
        connectSignalR,
        disconnectSignalR,
        initializeAppData, // 可以暴露给外部手动调用（例如加载存档后），但主要由内部连接成功时触发
    };
});