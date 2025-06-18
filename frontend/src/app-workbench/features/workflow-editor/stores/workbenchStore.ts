// --- START OF FILE frontend/src/app-workbench/features/workflow-editor/stores/workbenchStore.ts ---

import {defineStore} from 'pinia';
import {computed, type Ref, ref, shallowReadonly} from 'vue';
import {v4 as uuidv4} from 'uuid';
import {useMessage} from 'naive-ui';
import {
    EditSession,
    type ConfigType,
    type ConfigObject,
} from '@/app-workbench/features/workflow-editor/services/EditSession.ts';
import type {
    WorkflowProcessorConfig,
    StepProcessorConfig,
    AbstractModuleConfig,
} from '@/app-workbench/types/generated/workflow-config-api-client';
import {
    WorkflowConfigService,
    StepConfigService,
    ModuleConfigService,
} from '@/app-workbench/types/generated/workflow-config-api-client';
import {useAsyncResource} from "@/app-workbench/features/workflow-editor/composables/useAsyncResource.ts";
import type {AsyncData} from "@/types/asyncData.ts";

// 导出类型
export type WorkbenchStore = ReturnType<typeof useWorkbenchStore>;

// 这是一个辅助函数，建议放在通用的工具文件中 (e.g., /utils/clone.ts)
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
            for (const key in current) {
                if (Object.prototype.hasOwnProperty.call(current, key)) {
                    refresh(current[key]);
                }
            }
        }
    }

    refresh(newObj);
    return newObj;
}

// 定义草稿对象的内部结构
interface Draft {
    type: ConfigType;
    data: ConfigObject;
    originalState: string;
    uiState: {
        selectedItemId: string | null;
        expandedStepIds: string[];
    };
}

export const useWorkbenchStore = defineStore('workbench', () => {
    // =================================================================
    // 内部 State (完全封装)
    // =================================================================
    // --- 全局数据源 ---
    const globalWorkflows = ref<Record<string, WorkflowProcessorConfig>>({});
    const globalSteps = ref<Record<string, StepProcessorConfig>>({});
    const globalModules = ref<Record<string, AbstractModuleConfig>>({});

    // --- 共享的全局加载与错误状态 ---
    const _isLoadingGlobals = ref(false);
    const _fetchError = ref<any | null>(null);
    const _hasFetchedGlobals = ref(false);

    /**
     * 存储所有活跃的草稿会话。
     * Key 是临时的 draftId (UUID)。
     */
    const drafts = ref<Record<string, Draft>>({});

    /**
     * 跟踪已被锁定的全局配置，防止重复编辑。
     * Key 是全局配置的ID，Value 是对应的 draftId。
     */
    const lockedGlobalIds = ref<Record<string, string>>({});


    // =================================================================
    // 内部 Action
    // =================================================================

    /**
     * @internal - 真正执行数据拉取的私有函数。
     */
    async function _fetchAllGlobals() {
        // 防止并发调用
        if (_isLoadingGlobals.value) return;

        _isLoadingGlobals.value = true;
        _fetchError.value = null; // 重置错误状态
        try {
            const [workflows, steps, modules] = await Promise.all([
                WorkflowConfigService.getApiV1WorkflowsConfigsGlobalWorkflows(),
                StepConfigService.getApiV1WorkflowsConfigsGlobalSteps(),
                ModuleConfigService.getApiV1WorkflowsConfigsGlobalModules(),
            ]);
            globalWorkflows.value = workflows.reduce((acc, wf) => {
                acc[wf.name] = wf;
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

            // 拉取成功后，设置标志位
            _hasFetchedGlobals.value = true;
        } catch (error) {
            console.error('加载全局配置失败:', error);
            _fetchError.value = error; // 捕获错误
            useMessage().error('加载全局配置失败，请检查网络或联系管理员。');
        } finally {
            _isLoadingGlobals.value = false;
        }
    }

    /**
     * @internal - 一个确保全局数据已加载的“守卫”函数。
     * 任何需要访问全局数据的公共API都应该先调用它。
     */
    async function _ensureGlobalsFetched() {
        if (!_hasFetchedGlobals.value) {
            await _fetchAllGlobals();
        }
    }

    // =================================================================
    // 内部 Getter & Action (加下划线表示，约定不对外暴露)
    // =================================================================

    const _getDraftData = (draftId: string): ConfigObject | null => drafts.value[draftId]?.data ?? null;
    const _getUiState = (draftId: string) => drafts.value[draftId]?.uiState ?? null;

    const _isDirty = (type: ConfigType, draftId: string): boolean => {
        const draft = drafts.value[draftId];
        if (!draft) return false;
        return JSON.stringify(draft.data) !== draft.originalState;
    };

    const _updateDraftData = (draftId: string, updatedData: Partial<ConfigObject>) => {
        const draft = drafts.value[draftId];
        if (draft) {
            draft.data = {...draft.data, ...updatedData};
        }
    };

    const _setSelectedItemInDraft = (draftId: string, itemId: string | null) => {
        const uiState = _getUiState(draftId);
        if (uiState) uiState.selectedItemId = itemId;
    };

    const _toggleStepExpansionInDraft = (draftId: string, stepId: string) => {
        const uiState = _getUiState(draftId);
        if (!uiState) return;
        const index = uiState.expandedStepIds.indexOf(stepId);
        if (index > -1) {
            uiState.expandedStepIds.splice(index, 1);
        } else {
            uiState.expandedStepIds.push(stepId);
        }
    };

    const _saveDraft = async (type: ConfigType, draftId: string, globalId: string) => {
        const draft = drafts.value[draftId];
        if (!draft) return;

        const message = useMessage();
        let savePromise: Promise<any>;

        switch (type) {
            case 'workflow':
                savePromise = WorkflowConfigService.putApiV1WorkflowsConfigsGlobalWorkflows({
                    workflowId: globalId,
                    requestBody: draft.data as WorkflowProcessorConfig
                });
                break;
            case 'step':
                savePromise = StepConfigService.putApiV1WorkflowsConfigsGlobalSteps({
                    stepId: globalId,
                    requestBody: draft.data as StepProcessorConfig
                });
                break;
            case 'module':
                savePromise = ModuleConfigService.putApiV1WorkflowsConfigsGlobalModules({
                    moduleId: globalId,
                    requestBody: draft.data as AbstractModuleConfig
                });
                break;
            default:
                message.error('保存失败：未知的草稿类型。');
                return;
        }

        try {
            await savePromise;
            draft.originalState = JSON.stringify(draft.data); // 更新快照，重置isDirty
            await _fetchAllGlobals();
            message.success(`“${(draft.data as any).name}” 保存成功！`);
        } catch (error) {
            console.error(`保存 ${type} 草稿到后端时发生错误:`, error);
            message.error(`保存失败：与服务器通信时发生错误。`);
        }
    };

    const _closeDraft = (type: ConfigType, draftId: string, globalId: string): boolean => {
        if (_isDirty(type, draftId)) {
            if (!confirm('您有未保存的更改，确定要关闭吗？')) {
                return false;
            }
        }
        delete drafts.value[draftId];
        delete lockedGlobalIds.value[globalId];
        return true;
    };

    // =================================================================
    // “优雅的谎言”：为每种全局数据类型创建一个 AsyncData 实例
    // =================================================================

    /**
     * 创建一个看似独立的异步数据包。
     * @param dataRef - 指向全局数据源的 Ref (e.g., globalWorkflows)
     * @returns {AsyncData}
     */
    function createGlobalAsyncData<T>(dataRef: Ref<T>): AsyncData<T> {
        return {
            // data 是对全局数据源的只读引用，UI无法修改它
            data: shallowReadonly(dataRef),
            // isLoading 和 error 指向共享的全局状态
            isLoading: shallowReadonly(_isLoadingGlobals),
            error: shallowReadonly(_fetchError),
            // execute 方法调用共享的确保函数
            execute: _ensureGlobalsFetched,
        };
    }

    // =================================================================
    // 公共 API (暴露给外部世界的精简接口)
    // =================================================================

    /**
     * 获取（申请）一个编辑会话。这是进行任何修改操作的唯一入口。
     * @param type - 要编辑的配置类型
     * @param globalId - 全局配置的ID
     * @returns 如果成功，返回一个 EditSession 实例；如果已被锁定或找不到，返回 null。
     */
    async function acquireEditSession(type: ConfigType, globalId: string): Promise<EditSession | null> {
        // 确保全局数据已加载
        await _ensureGlobalsFetched();

        // 如果加载出错，则无法继续
        if (_fetchError.value) {
            useMessage().error('数据加载失败，无法开始编辑。');
            return null;
        }

        if (lockedGlobalIds.value[globalId]) {
            useMessage().warning(`“${globalId}” 正在被另一个会话编辑。`);
            return null;
        }

        let sourceConfig: ConfigObject | undefined;
        switch (type) {
            case 'workflow':
                sourceConfig = globalWorkflows.value[globalId];
                break;
            case 'step':
                sourceConfig = globalSteps.value[globalId];
                break;
            case 'module':
                sourceConfig = globalModules.value[globalId];
                break;
        }

        if (!sourceConfig) {
            useMessage().error(`找不到要编辑的全局配置项: ${globalId}`);
            return null;
        }

        const draftId = uuidv4();
        const draftData = deepCloneWithNewIds(sourceConfig);

        drafts.value[draftId] = {
            type: type,
            data: draftData,
            originalState: JSON.stringify(draftData),
            uiState: {
                selectedItemId: null,
                expandedStepIds: [],
            },
        };

        return new EditSession(type, draftId, globalId);
    }

    // =================================================================
    // 资源管理 (返回 AsyncData 对象)
    // =================================================================

    /**
     * 提供对全局工作流的访问。
     * 返回一个异步数据状态机，UI可以通过它来安全地消费数据。
     */
    const globalWorkflowsAsync: AsyncData<Record<string, WorkflowProcessorConfig>> = createGlobalAsyncData(globalWorkflows);

    /**
     * 提供对全局步骤的访问。
     */
    const globalStepsAsync: AsyncData<Record<string, StepProcessorConfig>> = createGlobalAsyncData(globalSteps);

    /**
     * 提供对全局模块的访问。
     */
    const globalModulesAsync: AsyncData<Record<string, AbstractModuleConfig>> = createGlobalAsyncData(globalModules);

    // 我们需要把所有 EditSession 需要的“私有”方法也 return 出去，
    // 这样注入的 storeInstance 才拥有这些方法。
    const internalApi = {
        _getDraftData,
        _getUiState,
        _isDirty,
        _updateDraftData,
        _setSelectedItemInDraft,
        _toggleStepExpansionInDraft,
        _saveDraft,
        _closeDraft,
    };

    return {
        // --- 只读数据访问器 ---
        globalWorkflowsAsync,
        globalStepsAsync,
        globalModulesAsync,

        // --- 核心服务方法 ---
        acquireEditSession,

        // --- 内部方法，供 EditSession 使用 ---
        // Vue 3 的 defineStore setup 语法不允许真正意义上的私有化，
        // 我们通过命名约定（下划线），来表示它们是内部API。
        ...internalApi
    };
});
// --- END OF FILE frontend/src/app-workbench/features/workflow-editor/stores/workbenchStore.ts ---