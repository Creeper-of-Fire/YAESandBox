import {defineStore} from 'pinia';
import {computed, type ComputedRef} from 'vue';
import {useWorkbenchStore, type WorkflowResourceItem} from '#/stores/workbenchStore';
import type {IWorkflowConfigProvider} from "@yaesandbox-frontend/core-services/injectKeys";

const STORAGE_KEY = 'workflowConfigProviderStore';

/**
 * @name useWorkflowConfigProviderStore
 * @description 这个 store 是一个适配器 (Adapter) 或包装器 (Wrapper)。
 * 它的唯一作用是连接底层的 `workbenchStore`，并将其复杂的内部状态
 * (`globalWorkflowsAsync`) 转换为一个干净、公开的、符合 `IWorkflowConfigProvider` 契约的接口。
 *
 * 这样做的好处是：
 * 1. **解耦**: 其他插件（如工作流执行器、可视化工具等）只需要依赖 `IWorkflowConfigProvider` 这个稳定的契约，
 *    而不需要知道 `workbenchStore` 的存在或其内部实现细节。
 * 2. **封装**: `workbenchStore` 的内部逻辑（如草稿、编辑会话等）被完全隐藏，只暴露必要的数据。
 *    为消费者提供它们所期望的精确数据类型。
 */
export const useWorkflowConfigProviderStore = defineStore(STORAGE_KEY, () =>
{
    const workbenchStore = useWorkbenchStore();

    // 从 workbenchStore 中获取原始的异步状态机
    const source = workbenchStore.globalWorkflowsAsync;

    // 创建一个计算属性来安全地将 source.state 转换为消费者期望的类型。
    // 由于 WorkflowConfig 和 RawWorkflowConfig 结构上是兼容的，这里可以直接进行类型断言。
    // 如果未来结构不兼容，这里将是进行数据转换（映射）的理想位置。
    const state: ComputedRef<Record<string, WorkflowResourceItem>> = computed(() => source.state as Record<string, WorkflowResourceItem>);

    // 直接代理（或包装在 computed 中）其他状态属性，以确保响应性
    const isLoading = computed(() => source.isLoading);
    const isReady = computed(() => source.isReady);
    const error = computed(() => source.error);

    // 转发 execute 方法
    const execute = async (): Promise<void> => {
        await source.execute();
    };

    // 返回的对象完全符合 IWorkflowConfigProvider 接口
    return {
        state,
        isLoading,
        isReady,
        error,
        execute,
    };
});
