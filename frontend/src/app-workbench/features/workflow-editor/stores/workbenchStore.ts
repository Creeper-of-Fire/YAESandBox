// --- START OF FILE frontend/src/app-workbench/features/workflow-editor/stores/workbenchStore.ts ---

import {defineStore} from 'pinia';
import {ref} from 'vue';
import {v4 as uuidv4} from 'uuid';
import {type ConfigObject, type ConfigType, EditSession,} from '@/app-workbench/features/workflow-editor/services/EditSession.ts';
import type {
    AbstractModuleConfig,
    StepProcessorConfig,
    WorkflowProcessorConfig,
} from '@/app-workbench/types/generated/workflow-config-api-client';
import {ModuleConfigService, StepConfigService, WorkflowConfigService,} from '@/app-workbench/types/generated/workflow-config-api-client';
import {useAsyncState} from "@vueuse/core";
import type {GlobalResourceItem} from "@/types/ui.ts";

// 导出类型
export type WorkbenchStore = ReturnType<typeof useWorkbenchStore>;

// 为不同类型的资源创建具体的别名，方便使用
export type WorkflowResourceItem = GlobalResourceItem<WorkflowProcessorConfig>;
export type StepResourceItem = GlobalResourceItem<StepProcessorConfig>;
export type ModuleResourceItem = GlobalResourceItem<AbstractModuleConfig>;

/**
 * @internal
 * 定义一个通用的 DTO 形状接口。
 * 任何对象，只要它拥有这些属性，就被认为是符合这个结构的。
 * 这利用了 TypeScript 的结构化类型系统。
 */
interface IJsonResultDto<T> {
    isSuccess: boolean;
    data: T;
    errorMessage: string | null;
    originJsonString: string | null;
}

/**
 * 辅助函数：将后端返回的 DTO 转换为我们前端的统一视图模型数组
 */
function processDtoToViewModel<T>(dto: Record<string, IJsonResultDto<T>>): Record<string, GlobalResourceItem<T>> {
    const viewModelItems: Record<string, GlobalResourceItem<T>> = {};
    for (const id in dto) {
        const item = dto[id];
        if (item.isSuccess && item.data) {
            viewModelItems[id] = {isSuccess: true, data: item.data};
        } else {
            viewModelItems[id] = {
                isSuccess: false,
                errorMessage: item.errorMessage || '未知错误，配置已损坏',
                originJsonString: item.originJsonString,
            }
            console.warn(`加载 ID 为 '${id}' 的全局配置失败: ${item.errorMessage}`);
        }
    }
    return viewModelItems;
}

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
    // const globalWorkflows = ref<Record<string, WorkflowProcessorConfig>>({});
    // const globalSteps = ref<Record<string, StepProcessorConfig>>({});
    // const globalModules = ref<Record<string, AbstractModuleConfig>>({});

    const globalWorkflowsAsync = useAsyncState(
        () => WorkflowConfigService.getApiV1WorkflowsConfigsGlobalWorkflows()
            .then(processDtoToViewModel),
        {} as Record<string, WorkflowResourceItem>,
        {immediate: false, shallow: true}
    );

    const globalStepsAsync = useAsyncState(
        () => StepConfigService.getApiV1WorkflowsConfigsGlobalSteps()
            .then(processDtoToViewModel),
        {} as Record<string, StepResourceItem>,
        {immediate: false, shallow: true}
    );

    const globalModulesAsync = useAsyncState(
        () => ModuleConfigService.getApiV1WorkflowsConfigsGlobalModules()
            .then(processDtoToViewModel),
        {} as Record<string, ModuleResourceItem>,
        {immediate: false, shallow: true}
    );

    // --- 共享的全局加载与错误状态 ---
    const _isLoadingGlobals = ref(false);
    const _fetchError = ref<any | null>(null);

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

    // /**
    //  * @internal - 真正执行数据拉取的私有函数。
    //  * 处理从后端返回的、包含成功与失败项的字典。
    //  */
    // async function _fetchAllGlobals() {
    //     // 防止并发调用
    //     if (_isLoadingGlobals.value) return;
    //
    //     _isLoadingGlobals.value = true;
    //     _fetchError.value = null; // 重置错误状态
    //     try {
    //         // 1. 并行调用所有获取全局配置的API
    //         const [workflowsResult, stepsResult, modulesResult] =
    //             await Promise.all([
    //                 WorkflowConfigService.getApiV1WorkflowsConfigsGlobalWorkflows(),
    //                 StepConfigService.getApiV1WorkflowsConfigsGlobalSteps(),
    //                 ModuleConfigService.getApiV1WorkflowsConfigsGlobalModules(),
    //             ]);
    //
    //         // --- 2. 处理工作流结果 ---
    //         const successfulWorkflows: Record<string, WorkflowProcessorConfig> = {};
    //         for (const id in workflowsResult) {
    //             const item = workflowsResult[id];
    //             if (item.isSuccess && item.data) {
    //                 // 只将成功加载并且数据存在的项存入
    //                 successfulWorkflows[id] = item.data;
    //             } else {
    //                 // 对于加载失败的项，在控制台打印警告
    //                 console.warn(`加载全局工作流 '${id}' 失败: ${item.errorMessage}`);
    //             }
    //         }
    //         globalWorkflows.value = successfulWorkflows;
    //
    //         // --- 3. 处理步骤结果 ---
    //         const successfulSteps: Record<string, StepProcessorConfig> = {};
    //         for (const id in stepsResult) {
    //             const item = stepsResult[id];
    //             if (item.isSuccess && item.data) {
    //                 successfulSteps[id] = item.data;
    //             } else {
    //                 console.warn(`加载全局步骤 '${id}' 失败: ${item.errorMessage}`);
    //             }
    //         }
    //         globalSteps.value = successfulSteps;
    //
    //         // --- 4. 处理模块结果 ---
    //         const successfulModules: Record<string, AbstractModuleConfig> = {};
    //         for (const id in modulesResult) {
    //             const item = modulesResult[id];
    //             if (item.isSuccess && item.data) {
    //                 successfulModules[id] = item.data;
    //             } else {
    //                 console.warn(`加载全局模块 '${id}' 失败: ${item.errorMessage}`);
    //             }
    //         }
    //         globalModules.value = successfulModules;
    //
    //         // 5. 拉取过程完成后，设置标志位
    //         _hasFetchedGlobals.value = true;
    //
    //     } catch (error) {
    //         // 这个 catch 块现在主要捕获顶层的网络错误或API服务器完全不可达的错误
    //         console.error('加载全局配置时发生严重错误:', error);
    //         _fetchError.value = error; // 捕获错误
    //         useMessage().error('加载全局配置失败，请检查网络或联系管理员。');
    //     } finally {
    //         _isLoadingGlobals.value = false;
    //     }
    // }

    // /**
    //  * @internal - 一个确保全局数据已加载的“守卫”函数。
    //  * 任何需要访问全局数据的公共API都应该先调用它。
    //  */
    // async function _ensureGlobalsFetched() {
    //     if (!_hasFetchedGlobals.value) {
    //         await _fetchAllGlobals();
    //     }
    // }

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

        let savePromise: Promise<any>;
        let refreshPromise: Promise<any>;

        switch (type) {
            case 'workflow':
                savePromise = WorkflowConfigService.putApiV1WorkflowsConfigsGlobalWorkflows({
                    workflowId: globalId,
                    requestBody: draft.data as WorkflowProcessorConfig
                });
                refreshPromise = globalWorkflowsAsync.execute(0);
                break;
            case 'step':
                savePromise = StepConfigService.putApiV1WorkflowsConfigsGlobalSteps({
                    stepId: globalId,
                    requestBody: draft.data as StepProcessorConfig
                });
                refreshPromise = globalStepsAsync.execute(0);
                break;
            case 'module':
                savePromise = ModuleConfigService.putApiV1WorkflowsConfigsGlobalModules({
                    moduleId: globalId,
                    requestBody: draft.data as AbstractModuleConfig
                });
                refreshPromise = globalModulesAsync.execute(0);
                break;
            default:
                console.error('保存失败：未知的草稿类型。');
                return;
        }

        try {
            await savePromise;
            draft.originalState = JSON.stringify(draft.data); // 更新快照，重置isDirty
            await refreshPromise;
            // message.success(`“${(draft.data as any).name}” 保存成功！`);
        } catch (error) {
            console.error(`保存 ${type} 草稿到后端时发生错误:`, error);
            // message.error(`保存失败：与服务器通信时发生错误。`);
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

    // /**
    //  * 创建一个看似独立的异步数据包。
    //  * @param dataRef - 指向全局数据源的 Ref (e.g., globalWorkflows)
    //  * @returns {AsyncData}
    //  */
    // function createGlobalAsyncData<T>(dataRef: Ref<T>): AsyncData<T> {
    //     return {
    //         // data 是对全局数据源的只读引用，UI无法修改它
    //         data: shallowReadonly(dataRef),
    //         // isLoading 和 error 指向共享的全局状态
    //         isLoading: shallowReadonly(_isLoadingGlobals),
    //         error: shallowReadonly(_fetchError),
    //         // execute 方法调用共享的确保函数
    //         execute: _ensureGlobalsFetched,
    //     };
    // }

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

        // 1. 检查这个 ID 是否已经被锁定
        if (lockedGlobalIds.value[globalId]) {
            // 找到已存在的 draft，可以给出更具体的提示（可选）
            // const existingDraftId = lockedGlobalIds.value[globalId];
            // const existingDraftName = drafts.value[existingDraftId]?.data?.name;
            console.warn(`“${globalId}” 正在被另一个会话编辑。`);
            return null;
        }

        // 2. 根据类型，选择对应的异步状态对象
        let stateObject: ReturnType<typeof useAsyncState<Record<string, WorkflowResourceItem | StepResourceItem | ModuleResourceItem>, any>>;
        switch (type) {
            case 'workflow':
                stateObject = globalWorkflowsAsync;
                break;
            case 'step':
                stateObject = globalStepsAsync;
                break;
            case 'module':
                stateObject = globalModulesAsync;
                break;
            default:
                // 不太可能发生，但作为防御性编程
                console.error(`未知的配置类型: ${type}`);
                return null;
        }

        // 3. 确保相关数据已加载
        // 如果 isReady 是 false，说明数据还没加载过或正在加载中
        // 我们需要等待它完成。execute() 在已加载或加载中时是安全的，它会返回当前的 promise。
        if (!stateObject.isReady.value) {
            console.log(`'${type}' 数据未就绪，开始加载...`);
            // execute() 自身会处理正在加载中的情况，所以直接 await 即可
            await stateObject.execute();
        }

        // 4. 检查加载是否出错
        if (stateObject.error.value) {
            // message.error(`获取 '${type}' 列表时发生错误，无法开始编辑。`);
            console.error(`获取 '${type}' 列表时发生错误：`, stateObject.error.value);
            return null;
        }

        // 5. 从新的数据结构 (GlobalResourceItem[]) 中查找源数据
        const sourceData = stateObject.state.value;
        const sourceItem = sourceData[globalId];

        if (!sourceItem) {
            console.error(`在 '${type}' 列表中找不到 ID 为 '${globalId}' 的配置项。`);
            return null;
        }

        // 6. 检查找到的项是否已损坏
        if (!sourceItem.isSuccess) {
            console.error(`配置项 '${globalId}' 已损坏，无法编辑。错误: ${sourceItem.errorMessage}`);
            return null;
        }

        // --- 如果一切正常，开始创建草稿 ---
        const sourceConfig = sourceItem.data; // 现在我们拿到了干净的数据
        const draftId = uuidv4();
        const draftData = deepCloneWithNewIds(sourceConfig); // 深度克隆，避免修改原始 store 数据

        drafts.value[draftId] = {
            type: type,
            data: draftData,
            originalState: JSON.stringify(draftData), // 创建快照用于比对 isDirty
            uiState: {
                selectedItemId: null,
                expandedStepIds: [],
            },
        };

        // 7. 标记此 globalId 已被锁定
        lockedGlobalIds.value[globalId] = draftId;

        // message.success(`开始编辑 “${draftData.name}”`);
        return new EditSession(type, draftId, globalId);
    }

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