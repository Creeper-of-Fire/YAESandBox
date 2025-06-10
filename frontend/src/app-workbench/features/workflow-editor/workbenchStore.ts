// --- START OF FILE frontend/src/app-workbench/features/workflow-editor/stores/workbenchStore.ts ---

import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { v4 as uuidv4 } from 'uuid';
import type {
    WorkflowProcessorConfig,
    StepProcessorConfig,
    AbstractModuleConfig,
} from '@/app-workbench/types/generated/workflow-config-api-client'; // 确保你的类型生成路径正确
import {
    WorkflowConfigService,
    StepConfigService,
    ModuleConfigService,
} from '@/app-workbench/types/generated/workflow-config-api-client'; // 确保你的服务生成路径正确

// 这是一个辅助函数，你需要在一个工具文件中实现它。
// 它的作用是深拷贝一个对象，并为所有嵌套的 configId 字段生成新的 UUID。
function deepCloneWithNewIds<T extends object>(obj: T): T {
    const newObj = JSON.parse(JSON.stringify(obj));

    function refresh(current: any) {
        if (typeof current !== 'object' || current === null) return;

        if (current.configId && typeof current.configId === 'string') {
            current.configId = uuidv4();
        }

        if (Array.isArray(current)) {
            current.forEach(refresh);
        } else if (typeof current === 'object') {
            Object.values(current).forEach(refresh);
        }
    }

    refresh(newObj);
    return newObj;
}


export const useWorkbenchStore = defineStore('workbench', () => {
    // =================================================================
    // State
    // =================================================================

    /**
     * 全局工作流配置库，从后端加载。
     * Key 是工作流的持久化 ID (例如，文件名或数据库主键)。
     */
    const globalWorkflows = ref<Record<string, WorkflowProcessorConfig>>({});

    /**
     * 全局步骤配置库，从后端加载。
     * Key 是步骤的持久化 ID。
     */
    const globalSteps = ref<Record<string, StepProcessorConfig>>({});

    /**
     * 全局模块配置库，从后端加载。
     * Key 是模块的持久化 ID。
     */
    const globalModules = ref<Record<string, AbstractModuleConfig>>({});

    /**
     * 正在编辑的工作流草稿列表。
     * Key 是一个临时的草稿 ID (UUID)，用于在前端标识不同的标签页/草稿实例。
     */
    const workflowDrafts = ref<Record<string, WorkflowProcessorConfig>>({});

    /**
     * 存储每个草稿打开时的原始状态（JSON字符串）。
     * 用于与当前状态对比，判断是否“脏”。
     * Key 是临时的草稿 ID。
     */
    const originalDrafts = ref<Record<string, string>>({});

    /**
     * 是否正在从后端加载全局配置。
     */
    const isLoadingGlobals = ref(false);


    // =================================================================
    // Getter
    // =================================================================
    /**
     * isDirty getter 需要接收一个 draftId 作为参数。
     */
    const isDirty = computed(() => (draftId: string): boolean => {
        const draft = workflowDrafts.value[draftId];
        if (!draft) return false;

        const originalState = originalDrafts.value[draftId];
        const currentState = JSON.stringify(draft);

        return originalState !== currentState;
    });

    // =================================================================
    // Actions
    // =================================================================

    /**
     * 从后端拉取所有全局配置（工作流、步骤、模块）。
     * 通常在应用启动时调用一次。
     */
    async function fetchAllGlobals() {
        isLoadingGlobals.value = true;
        try {
            const [workflows, steps, modules] = await Promise.all([
                WorkflowConfigService.getApiV1WorkflowsConfigsGlobalWorkflows(),
                StepConfigService.getApiV1WorkflowsConfigsGlobalSteps(),
                ModuleConfigService.getApiV1WorkflowsConfigsGlobalModules(),
            ]);

            // 将数组转换为 Record<id, object> 格式，便于快速查找
            globalWorkflows.value = workflows.reduce((acc, wf) => {
                acc[wf.name] = wf; // 假设工作流的持久化ID是它的名字
                return acc;
            }, {} as Record<string, WorkflowProcessorConfig>);

            globalSteps.value = steps.reduce((acc, step) => {
                acc[step.configId] = step;
                return acc;
            }, {} as Record<string, StepProcessorConfig>);

            globalModules.value = modules.reduce((acc, mod) => {
                acc[mod.configId] = mod;
                return acc;
            }, {} as Record<string, AbstractModuleConfig>);

        } catch (error) {
            console.error('加载全局配置失败:', error);
            // 在这里可以添加全局错误通知
        } finally {
            isLoadingGlobals.value = false;
        }
    }

    /**
     * 将一个全局工作流作为新的草稿打开进行编辑。
     * @param workflowId 要打开的全局工作流的 ID。
     */
    function openWorkflowAsDraft(workflowId: string):string | null {
        const globalWorkflow = globalWorkflows.value[workflowId];
        if (!globalWorkflow) {
            console.error(`无法找到ID为 ${workflowId} 的全局工作流。`);
            return null;
        }

        // 使用深拷贝并刷新所有内部ID，确保草稿是完全独立的
        const draft = deepCloneWithNewIds(globalWorkflow);
        const draftId = uuidv4(); // 为这个编辑会话创建一个唯一的ID

        workflowDrafts.value[draftId] = draft;
        originalDrafts.value[draftId] = JSON.stringify(draft); // 存储原始状态
        return draftId;
    }

    /**
     * 创建一个全新的、空的工作流草稿。
     */
    function createNewWorkflowDraft():string {
        const draftId = uuidv4();
        const newWorkflow: WorkflowProcessorConfig = {
            name: `新工作流-${draftId.substring(0, 4)}`, // 提供一个唯一的默认名
            enabled: true,
            triggerParams: [],
            steps: [],
        };

        workflowDrafts.value[draftId] = newWorkflow;
        originalDrafts.value[draftId] = JSON.stringify(newWorkflow);
        return draftId;
    }

    /**
     * 将当前激活的草稿保存到后端。
     */
    async function saveActiveDraft(draftId: string) {
        const draftToSave = workflowDrafts.value[draftId];
        if (!draftToSave) return;

        try {
            await WorkflowConfigService.putApiV1WorkflowsConfigsGlobalWorkflows({
                workflowId: draftToSave.name, // 假设工作流的持久化ID是它的名字
                requestBody: draftToSave,
            });

            // 保存成功后，更新原始状态，重置 isDirty 标记
            originalDrafts.value[draftId] = JSON.stringify(draftToSave);

            // 可选：重新拉取全局配置以保持同步
            await fetchAllGlobals();

            console.log(`工作流 "${draftToSave.name}" 保存成功！`);
            // 在此可以触发一个成功的通知

        } catch (error) {
            console.error('保存工作流失败:', error);
            // 在此触发一个失败的通知
        }
    }

    /**
     * 关闭一个草稿。
     * @param draftId 要关闭的草稿的 ID。
     */
    function closeDraft(draftId: string) {
        const draftIsDirty = JSON.stringify(workflowDrafts.value[draftId]) !== originalDrafts.value[draftId];
        if (draftIsDirty) {
            if (!confirm('您有未保存的更改，确定要关闭吗？')) {
                return;
            }
        }

        delete workflowDrafts.value[draftId];
        delete originalDrafts.value[draftId];
    }

    return {
        // State
        globalWorkflows,
        globalSteps,
        globalModules,
        workflowDrafts,
        isLoadingGlobals,

        // Getters
        isDirty,

        // Actions
        fetchAllGlobals,
        openWorkflowAsDraft,
        createNewWorkflowDraft,
        saveActiveDraft,
        closeDraft,
    };
});
// --- END OF FILE frontend/src/app-workbench/features/workflow-editor/stores/workbenchStore.ts ---