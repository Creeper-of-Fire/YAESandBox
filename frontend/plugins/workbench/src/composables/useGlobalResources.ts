import { computed, onMounted, type Ref } from 'vue';
import {type ConfigTypeMap, type GlobalResourceItemSuccess, useWorkbenchStore} from '#/stores/workbenchStore';
import type { AnyConfigObject, ConfigType } from '#/services/GlobalEditSession';
import type { GlobalResourceItem } from '#/stores/workbenchStore';

/**
 * 一个通用的、响应式的 Composable，用于获取全局资源列表。
 * 它会自动将后端的持久化数据与当前活跃的编辑草稿进行合并，
 * 始终为UI提供最新、最准确的数据视图。
 *
 * @param type - 要获取的资源类型 ('workflow', 'tuum', 'rune')
 * @returns 返回一个包含合并后资源、加载状态和错误状态的对象。
 */
export function useGlobalResources<K extends ConfigType>(type: K) {
    type T = ConfigTypeMap[K];
    const workbenchStore = useWorkbenchStore();

    // 1. 从 store 的资源处理器中获取对应类型的异步状态管理器
    const getAsyncStateForType = () => {
        switch (type) {
            case 'workflow': return workbenchStore.globalWorkflowsAsync;
            case 'tuum': return workbenchStore.globalTuumsAsync;
            case 'rune': return workbenchStore.globalRunesAsync;
        }
    };
    const asyncState = getAsyncStateForType();

    // 2. 自动在组件挂载时执行数据获取（如果需要）
    onMounted(() => {
        if (!asyncState.isReady && !asyncState.isLoading) {
            asyncState.execute();
        }
    });

    // 3. 核心：创建响应式的合并视图
    const resources: Ref<Record<string, GlobalResourceItem<T>>> = computed(() => {
        // a. 以服务器返回的数据为基础
        const baseResources = { ...asyncState.state } as Record<string, GlobalResourceItem<T>>;

        // b. 获取所有活跃的会话（草稿）
        const activeSessions = workbenchStore.getActiveSessions;

        // c. 遍历草稿，用草稿版本覆盖基础数据
        for (const storeId in activeSessions) {
            const session = activeSessions[storeId];

            // 只覆盖与当前 composable 类型匹配的草稿
            if (session.type === type) {
                // getFullDraft() 返回的是一个完整的 GlobalResourceItemSuccess 对象
                baseResources[storeId] = session.getFullDraft().value as GlobalResourceItem<T>;
            }
        }

        return baseResources as Record<string, GlobalResourceItem<T>>;
    });

    // 4. 将最终的视图和状态暴露给组件
    return {
        /**
         * 合并后的资源列表 (Record<storeId, GlobalResourceItem>)。
         * 这是一个计算属性，对草稿或后端数据的任何更改都会使其自动更新。
         */
        resources,
        /**
         * 底层数据是否正在加载中。
         */
        isLoading: computed(() => asyncState.isLoading),
        /**
         * 底层数据加载是否出错。
         */
        error: computed(() => asyncState.error),
        /**
         * 手动触发数据重新加载的方法。
         */
        execute: () => asyncState.execute(),
    };
}