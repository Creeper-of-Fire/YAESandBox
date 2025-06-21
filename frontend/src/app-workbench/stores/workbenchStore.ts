// --- START OF FILE frontend/src/app-workbench/stores/workbenchStore.ts ---

import {defineStore} from 'pinia';
import {computed, ref} from 'vue';
import {v4 as uuidv4} from 'uuid';
import {type ConfigObject, type ConfigType, EditSession,} from '@/app-workbench/services/EditSession.ts';
import type {
    AbstractModuleConfig,
    StepProcessorConfig,
    WorkflowProcessorConfig,
} from '@/app-workbench/types/generated/workflow-config-api-client';
import {ModuleConfigService, StepConfigService, WorkflowConfigService,} from '@/app-workbench/types/generated/workflow-config-api-client';
import {useAsyncState} from "@vueuse/core";
import type {GlobalResourceItem} from "@/types/ui.ts";
import {cloneDeep} from "lodash-es";

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
export function deepCloneWithNewIds<T extends object>(obj: T): T {
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
}

export const useWorkbenchStore = defineStore('workbench', () => {
    // =================================================================
    // 内部 State (完全封装)
    // =================================================================

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
    // 内部 Getter & Action (加下划线表示，约定不对外暴露)
    // =================================================================

    // 方法现在接收 globalId
    const _getDraftData = (globalId: string): ConfigObject | null => drafts.value[globalId]?.data ?? null;

    // 所有 UI State 相关的方法 (_getUiState, _setSelectedItemInDraft, _toggleStepExpansionInDraft) 已被彻底移除。

    const _isDirty = (globalId: string): boolean => {
        const draft = drafts.value[globalId];
        if (!draft) return false;
        return JSON.stringify(draft.data) !== draft.originalState;
    };

    const _updateDraftData = (globalId: string, updatedData: Partial<ConfigObject>) => {
        const draft = drafts.value[globalId];
        if (draft) {
            draft.data = {...draft.data, ...updatedData};
        }
    };

    // _saveDraft 现在接收 globalId
    const _saveDraft = async (type: ConfigType, globalId: string) => {
        const draft = drafts.value[globalId];
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
            draft.originalState = JSON.stringify(draft.data);
            await refreshPromise;
        } catch (error) {
            console.error(`保存 ${type} 草稿到后端时发生错误:`, error);
        }
    };

    /**
     * _closeDraft 重命名为 _discardDraft，并且不再有确认提示。
     * 它的作用是简单地、无条件地丢弃一个草稿。
     * @param globalId - 要丢弃的草稿的全局ID。
     */
    const _discardDraft = (globalId: string) => {
        delete drafts.value[globalId];
    };

    // =================================================================
    // 公共 API (暴露给外部世界的精简接口)
    // =================================================================

    /**
     * 新增计算属性，用于判断整个工作台是否存在任何未保存的更改。
     * 这将用于在用户关闭浏览器标签页时发出警告。
     */
    const hasDirtyDrafts = computed(() => {
        return Object.keys(drafts.value).some(globalId => _isDirty(globalId));
    });

    /**
     * 获取（申请）一个编辑会话。这是进行任何修改操作的唯一入口。
     * @param type - 要编辑的配置类型
     * @param globalId - 全局配置的ID
     * @returns 如果成功，返回一个 EditSession 实例；如果已被锁定或找不到，返回 null。
     */
    async function acquireEditSession(type: ConfigType, globalId: string): Promise<EditSession | null> {

        // 检查是否已存在同名草稿
        if (drafts.value[globalId]) {
            console.warn(`“${globalId}” 正在被编辑。`);
            // 如果需要，可以返回已存在的会话实例
            return new EditSession(type, globalId);
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

        // 因为是请求编辑，所以不会对ConfigID进行修改。如果需要修改，请在外面先改好。
        const draftData = cloneDeep(sourceConfig); // 深度克隆，避免修改原始 store 数据

        // 使用 globalId 作为键
        drafts.value[globalId] = {
            type: type,
            data: draftData,
            originalState: JSON.stringify(draftData), // 创建快照
        };

        // 返回只包含 type 和 globalId 的会话
        return new EditSession(type, globalId);
    }

    // 我们需要把所有 EditSession 需要的“私有”方法也 return 出去，
    // 这样注入的 storeInstance 才拥有这些方法。
    const internalApi = {
        _getDraftData,
        _isDirty,
        _updateDraftData,
        _saveDraft,
        _discardDraft,
    };

    return {
        // --- 只读数据访问器 ---
        globalWorkflowsAsync,
        globalStepsAsync,
        globalModulesAsync,

        hasDirtyDrafts,

        // --- 核心服务方法 ---
        acquireEditSession,

        // --- 内部方法，供 EditSession 使用 ---
        // Vue 3 的 defineStore setup 语法不允许真正意义上的私有化，
        // 我们通过命名约定（下划线），来表示它们是内部API。
        ...internalApi
    };
});
// --- END OF FILE frontend/src/app-workbench/stores/workbenchStore.ts ---