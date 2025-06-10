// --- START OF FILE frontend/src/app-workbench/features/workflow-editor/stores/workflowEditorStore.ts ---

import {defineStore} from 'pinia';
import {computed, ref} from 'vue';
import {useDebounceFn} from '@vueuse/core';
import type {
    AbstractModuleConfig,
    ValidationMessage, WorkflowProcessorConfig,
    WorkflowValidationReport
} from '@/app-workbench/types/generated/workflow-config-api-client';
import {WorkflowAnalysisService} from '@/app-workbench/types/generated/workflow-config-api-client';
import {useWorkbenchStore} from './workbenchStore';

export const useWorkflowEditorStore = defineStore('workflow-editor', () => {
    // =================================================================
    // State
    // =================================================================

    /**
     * activeDraftId 现在在这里！
     * 它代表当前编辑器会话正在操作的草稿ID。
     */
    const activeDraftId = ref<string | null>(null);

    /**
     * 当前在编辑器中被选中的项（步骤或模块）的 configId。
     * 用于驱动中间栏的编辑器显示。
     */
    const selectedItemId = ref<string | null>(null);

    /**
     * 在左侧结构树中，当前处于展开状态的步骤的 configId 列表。
     */
    const expandedStepIds = ref<string[]>([]);

    /**
     * 从后端获取的当前工作流草稿的完整校验报告。
     */
    const validationReport = ref<WorkflowValidationReport | null>(null);

    /**
     * 是否正在调用后端的校验API。
     */
    const isValidating = ref(false);


    // =================================================================
    // Getters
    // =================================================================

    /**
     * 这是一个核心的联动 Getter，它从 workbenchStore 获取当前活动的草稿。
     * 本 Store 的所有操作都基于这个数据源。
     */
    const currentWorkflowDraft = computed<WorkflowProcessorConfig | null>(() => {
        const workbench = useWorkbenchStore();
        return activeDraftId.value ? workbench.workflowDrafts[activeDraftId.value] : null;
    });

    /**
     * 关键改动：为UI提供一个便捷的 isDirty getter。
     */
    const isCurrentDraftDirty = computed<boolean>(() => {
        if (!activeDraftId.value) return false;
        return useWorkbenchStore().isDirty(activeDraftId.value);
    });

    /**
     * 获取当前选中的模块对象。
     * 这是一个方便的、带有类型守卫的 Getter，专门供模块编辑器使用。
     */
    const selectedModule = computed<AbstractModuleConfig | null>(() => {
        if (!selectedItemId.value || !currentWorkflowDraft.value) return null;

        for (const step of currentWorkflowDraft.value.steps) {
            const foundModule = step.modules.find(m => m.configId === selectedItemId.value);
            if (foundModule) return foundModule;
        }

        return null;
    });

    /**
     * 根据项的 ID 从校验报告中获取其对应的所有校验信息。
     * @param itemId - 步骤或模块的 configId
     * @returns 校验信息数组
     */
    const getValidationMessagesForItem = computed(() => (itemId: string): ValidationMessage[] => {
        if (!validationReport.value) return [];

        // 检查是否为步骤级别的消息
        if (validationReport.value.stepResults[itemId]) {
            return validationReport.value.stepResults[itemId].stepMessages;
        }

        // 检查是否为模块级别的消息
        for (const stepResult of Object.values(validationReport.value.stepResults)) {
            if (stepResult.moduleResults[itemId]) {
                return stepResult.moduleResults[itemId].moduleMessages;
            }
        }

        return [];
    });


    // =================================================================
    // Actions
    // =================================================================

    /**
     * 一个action，设置活动的草稿。
     * UI组件（如标签页）将调用这个action来切换编辑器目标。
     * @param draftId - 要设为激活的草稿ID，或null来关闭编辑器。
     */
    function setActiveDraft(draftId: string | null) {
        if (activeDraftId.value === draftId) return; // 避免不必要的切换
        activeDraftId.value = draftId;

        // 切换草稿后，重置UI状态
        selectedItemId.value = null;
        expandedStepIds.value = [];
        validationReport.value = null;

        // 立即触发一次校验
        if (draftId) {
            validateWorkflow();
        }
    }

    /**
     * 设置当前选中的项。
     * @param itemId - 要选中的项的 configId，如果为 null 则取消选择。
     */
    function selectItem(itemId: string | null) {
        selectedItemId.value = itemId;
    }

    /**
     * 切换一个步骤的展开/折叠状态。
     * @param stepId - 要操作的步骤的 configId。
     */
    function toggleStepExpansion(stepId: string) {
        const index = expandedStepIds.value.indexOf(stepId);
        if (index > -1) {
            expandedStepIds.value.splice(index, 1);
        } else {
            expandedStepIds.value.push(stepId);
        }
    }

    /**
     * （防抖）对当前工作流草稿进行静态校验。
     * 这个函数本身是防抖的，可以在任何草稿变动后被安全调用。
     */
    const validateWorkflow = useDebounceFn(async () => {
        const draft = currentWorkflowDraft.value;
        if (!draft) {
            validationReport.value = null;
            return;
        }

        isValidating.value = true;
        try {
            validationReport.value = await WorkflowAnalysisService.postApiV1WorkflowsConfigsAnalysisValidateWorkflow({
                requestBody: draft,
            });
        } catch (error) {
            console.error('工作流校验失败:', error);
            validationReport.value = null; // 出错时清空报告
        } finally {
            isValidating.value = false;
        }
    }, 500); // 500ms 防抖延迟


    // 请注意：所有对草稿数据的增删改查操作，都应该在 workbenchStore 中实现，
    // 因为数据本身存放在那里。此处的 editorStore 只负责触发这些操作和管理UI状态。
    // 例如，删除一个模块的函数，其组件调用的是 `useWorkflowEditorStore().deleteItem(id)`，
    // 但具体实现可能是在 `useWorkbenchStore` 中修改 `activeDraft`。
    // 为保持逻辑清晰，也可以将修改逻辑直接放在`workbenchStore`的action中。

    return {
        // State
        selectedItemId,
        expandedStepIds,
        validationReport,
        isValidating,
        isCurrentDraftDirty,

        // Getters
        currentWorkflowDraft,
        selectedModule,
        getValidationMessagesForItem,

        // Actions
        setActiveDraft,
        selectItem,
        toggleStepExpansion,
        validateWorkflow,
    };
});
// --- END OF FILE frontend/src/app-workbench/features/workflow-editor/stores/workflowEditorStore.ts ---