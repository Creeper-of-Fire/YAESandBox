import {computed, inject, onMounted, ref} from 'vue';
import {useScopedStorage} from '../../composables/useScopedStorage.ts';
import type {WorkflowResourceItem} from '../../injectKeys.ts';
import {WorkflowConfigProviderKey} from '../../injectKeys.ts';
import type {WorkflowConfig} from "../../types";

export interface MappedWorkflow {
    id: string;
    resource: WorkflowConfig;
}

/**
 * 管理单个工作流选择和持久化的 Composable.
 * @param storageKey - 用于 useScopedStorage 的唯一局部键。
 */
export function useWorkflowSelector(storageKey: string)
{
    // 1. 注入全局的工作流提供者
    const workflowProvider = inject(WorkflowConfigProviderKey);

    // 2. 使用 useScopedStorage 持久化用户选择的工作流 ID
    // 这个 ID 会在同一作用域下的不同组件实例间共享（如果它们使用相同的 storageKey）
    const selectedWorkflowId = useScopedStorage<string | null>(storageKey, null, localStorage, 'WorkflowSelector');

    // 3. 组件挂载时确保工作流列表已加载
    onMounted(async () =>
    {
        if (workflowProvider && !workflowProvider.isReady.value)
        {
            await workflowProvider.execute();
        }
    });

    // 4. 计算属性：从全局提供者中筛选出所有可用的工作流
    const availableWorkflows = computed<MappedWorkflow[]>(() =>
    {
        if (!workflowProvider || !workflowProvider.isReady.value)
        {
            return [];
        }
        return Object.entries(workflowProvider.state.value).map(([id, resourceItem]) =>
        {
            if (!resourceItem.isSuccess)
                return null;
            return ({
                id: id,
                resource: resourceItem.data
            });
        }).filter((item): item is MappedWorkflow => item !== null);
    });

    // 5. 计算属性：根据存储的 ID，查找并返回完整的工作流配置对象
    const selectedWorkflowConfig = computed<WorkflowConfig | undefined>(() =>
    {
        if (!selectedWorkflowId.value || !workflowProvider)
            return undefined;

        const workflowConfig: WorkflowResourceItem | undefined = workflowProvider.state?.value[selectedWorkflowId.value];
        if (!workflowConfig || !workflowConfig.isSuccess)
            return undefined;
        return workflowConfig.data;
    });

    // 6. 操作函数
    function selectWorkflow(id: string)
    {
        selectedWorkflowId.value = id;
    }

    function clearSelection()
    {
        selectedWorkflowId.value = null;
    }

    return {
        // 状态 & 数据
        selectedWorkflowId,
        availableWorkflows,
        selectedWorkflowConfig,
        // 全局 Provider 的状态，方便 UI 显示加载中等
        isProviderReady: computed(() => workflowProvider?.isReady.value ?? false),
        isProviderLoading: computed(() => workflowProvider?.isLoading.value ?? false),

        // 方法
        selectWorkflow,
        clearSelection,
    };
}