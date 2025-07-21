// --- START OF FILE frontend/src/app-workbench/stores/workbenchStore.ts ---

import {defineStore} from 'pinia';
import {computed, reactive, ref} from 'vue';
import {v4 as uuidv4} from 'uuid';
import {type ConfigObject, type ConfigType, EditSession,} from '@/app-workbench/services/EditSession.ts';
import type {
    AbstractModuleConfig,
    ModuleSchemasResponse,
    StepProcessorConfig,
    WorkflowProcessorConfig,
} from '@/app-workbench/types/generated/workflow-config-api-client';
import {ModuleConfigService, StepConfigService, WorkflowConfigService,} from '@/app-workbench/types/generated/workflow-config-api-client';
import {useAsyncState} from "@vueuse/core";
import type {GlobalResourceItem} from "@/types/ui.ts";
import {cloneDeep} from "lodash-es";
import {type DynamicAsset, loadAndRegisterPlugins} from "@/app-workbench/features/schema-viewer/plugin-loader.ts";
import {preprocessSchemaForWidgets} from "@/app-workbench/features/schema-viewer/preprocessSchema.ts";

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
interface IJsonResultDto<T>
{
    isSuccess: boolean;
    data: T;
    errorMessage: string | null;
    originJsonString: string | null;
}

export interface SaveResult
{
    success: boolean;
    id: string;
    name?: string;
    type: ConfigType;
    error?: any; // 保存具体的错误信息
}

export interface WorkflowModuleRules
{
    noConfig?: boolean;
    singleInStep?: boolean;
    inLastStep?: boolean;
    inFrontOf?: string[]; // 存储的是模块的类型名 (e.g., "PromptGenerationModuleConfig")
    behind?: string[]; // 存储的是模块的类型名 (e.g., "PromptGenerationModuleConfig")
}

/**
 * 元数据接口，整合了规则和类别标签。
 * 这提供了一个统一的元数据访问点。
 */
export interface ModuleMetadata
{
    rules?: WorkflowModuleRules;
    classLabel?: string;
}

/**
 * 辅助函数：将后端返回的 DTO 转换为我们前端的统一视图模型数组
 */
function processDtoToViewModel<T>(dto: Record<string, IJsonResultDto<T>>): Record<string, GlobalResourceItem<T>>
{
    const viewModelItems: Record<string, GlobalResourceItem<T>> = {};
    for (const id in dto)
    {
        const item = dto[id];
        if (item.isSuccess && item.data)
        {
            viewModelItems[id] = {isSuccess: true, data: item.data};
        }
        else
        {
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
export function deepCloneWithNewIds<T extends object>(obj: T): T
{
    const newObj = JSON.parse(JSON.stringify(obj));

    function refresh(current: any)
    {
        if (typeof current !== 'object' || current === null) return;
        if (current.configId && typeof current.configId === 'string')
        {
            current.configId = uuidv4();
        }
        if (Array.isArray(current))
        {
            current.forEach(refresh);
        }
        else if (typeof current === 'object')
        {
            for (const key in current)
            {
                if (Object.prototype.hasOwnProperty.call(current, key))
                {
                    refresh(current[key]);
                }
            }
        }
    }

    refresh(newObj);
    return newObj;
}

// 定义草稿对象的内部结构
interface Draft
{
    type: ConfigType;
    data: ConfigObject;
    originalState: string;
}

export const useWorkbenchStore = defineStore('workbench', () =>
{
    // =================================================================
    // 内部 State (完全封装)
    // =================================================================

    const globalWorkflowsAsync = reactive(useAsyncState(
        () => WorkflowConfigService.getApiV1WorkflowsConfigsGlobalWorkflows()
            .then(processDtoToViewModel),
        {} as Record<string, WorkflowResourceItem>,
        {immediate: false, shallow: false}
    ));

    const globalStepsAsync = reactive(useAsyncState(
        () => StepConfigService.getApiV1WorkflowsConfigsGlobalSteps()
            .then(processDtoToViewModel),
        {} as Record<string, StepResourceItem>,
        {immediate: false, shallow: false}
    ));

    const globalModulesAsync = reactive(useAsyncState(
        () => ModuleConfigService.getApiV1WorkflowsConfigsGlobalModules()
            .then(processDtoToViewModel),
        {} as Record<string, ModuleResourceItem>,
        {immediate: false, shallow: false}
    ));

    /**
     * 存储所有模块类型的 Schema
     * Key 是模块的 moduleType, Value 是对应的 JSON Schema
     */
    const moduleSchemasAsync =  reactive(useAsyncState(
        async () =>
        { // <--- 重点修改：将 handler 函数改为 async
            console.log("正在获取模块Schema和动态资源...");
            const response: ModuleSchemasResponse = await ModuleConfigService.getApiV1WorkflowsConfigsGlobalModulesAllModuleConfigsSchemas();

            console.log("已获取后端响应，准备加载插件资源...");
            // 1. 加载并注册所有动态组件
            // loadAndRegisterPlugins 内部有去重和缓存，所以多次调用是安全的
            const dynamicAssetsFromBackend = response.dynamicAssets as DynamicAsset[];
            await loadAndRegisterPlugins(dynamicAssetsFromBackend);

            // console.log("插件资源加载完成，开始预处理Schema...");
            // // 2. 预处理 Schema，注入组件引用
            // const processedSchemas = preprocessSchemaForWidgets(response.schemas);
            //
            console.log("插件资源加载完成，返回给Store状态。");
            return response.schemas;
        },
        {} as Record<string, any>, // 初始值
        {immediate: false, shallow: false} // 初始不加载，由组件手动触发
    ));

    /**
     * 元数据服务。
     * 该计算属性提供了一个单一的、聚合的元数据来源。
     * 它会从每个模块的 Schema 中提取 `x-workflow-module-rules` 和 `classLabel` 等元数据信息。
     */
    const moduleMetadata = computed(() =>
    {
        const metadataMap: Record<string, ModuleMetadata> = {};
        const schemas = moduleSchemasAsync.state;

        if (schemas)
        {
            for (const moduleType in schemas)
            {
                const schema = schemas[moduleType];
                if (schema)
                {
                    const rules = schema['x-workflow-module-rules'] as WorkflowModuleRules | undefined;
                    // 从 Schema 中提取新的类别标签属性
                    const classLabel = schema['classLabel'] as string | undefined;

                    // 只要 Schema 中包含任何一个元数据，就为其创建一个条目
                    if (rules || classLabel)
                    {
                        const metadata: ModuleMetadata = {};
                        if (rules)
                        {
                            metadata.rules = rules;
                        }
                        if (classLabel)
                        {
                            metadata.classLabel = classLabel;
                        }
                        metadataMap[moduleType] = metadata;
                    }
                }
            }
        }
        return metadataMap;
    });

    /**
     * 存储所有活跃的草稿会话。
     * Key 是临时的 draftId (UUID)。
     */
    const drafts = ref<Record<string, Draft>>({});

    // =================================================================
    // 内部 Getter & Action (加下划线表示，约定不对外暴露)
    // =================================================================

    // 方法现在接收 globalId
    const _getDraftData = (globalId: string): ConfigObject | null => drafts.value[globalId]?.data ?? null;

    // 所有 UI State 相关的方法 (_getUiState, _setSelectedItemInDraft, _toggleStepExpansionInDraft) 已被彻底移除。

    const _isDirty = (globalId: string): boolean =>
    {
        const draft = drafts.value[globalId];
        if (!draft) return false;
        return JSON.stringify(draft.data) !== draft.originalState;
    };

    const _updateDraftData = (globalId: string, updatedData: Partial<ConfigObject>) =>
    {
        const draft = drafts.value[globalId];
        if (draft)
        {
            draft.data = {...draft.data, ...updatedData};
        }
    };

    // TODO 没写好校验逻辑
    // _saveDraft 现在接收 globalId
    const _saveDraft = async (type: ConfigType, globalId: string): Promise<SaveResult> =>
    {
        const draft = drafts.value[globalId];
        if (!draft)
            return {success: false, name: "【未找到草稿】", id: globalId, type, error: '草稿未找到'};

        let savePromise: Promise<any>;
        let refreshPromise: Promise<any>;

        switch (type)
        {
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
                return {success: false, name: draft.data.name, id: globalId, type, error: '未知的草稿类型'};
        }

        try
        {
            await savePromise;
            draft.originalState = JSON.stringify(draft.data);
            await refreshPromise;
            return {success: true, name: draft.data.name, id: globalId, type};
        } catch (error)
        {
            console.error(`保存 ${type} 草稿到后端时发生错误:`, error);
            return {success: false, name: draft.data.name, id: globalId, type, error};
        }
    };

    /**
     * _discardDraft 将草稿数据恢复为原始状态，而不是删除草稿。
     * 它的作用是撤销所有未保存的更改，使草稿回到初始状态。
     * @param globalId - 要放弃更改的草稿的全局ID。
     */
    const _discardDraft = (globalId: string) =>
    {
        const draft = drafts.value[globalId];
        if (draft)
        {
            // 将数据恢复为创建草稿时的原始状态
            draft.data = JSON.parse(draft.originalState);
        }
    };

    // =================================================================
    // 公共 API (暴露给外部世界的精简接口)
    // =================================================================

    /**
     * 将一个已有的配置对象保存为新的全局配置。
     * @param configToSave - 要保存为全局的配置对象。
     */
    async function createGlobalConfig(configToSave: ConfigObject)
    {
        // 1. 深克隆并确保ID是全新的（即使是克隆来的）
        const newGlobalConfig = deepCloneWithNewIds(configToSave);
        const newGlobalId = uuidv4(); // 为这个新的全局资源创建一个全新的顶级ID

        let type: ConfigType;
        let savePromise: Promise<any>;
        let refreshPromise: () => Promise<any>;

        // 2. 判断类型并准备API调用
        if ('steps' in newGlobalConfig)
        {
            type = 'workflow';
            savePromise = WorkflowConfigService.putApiV1WorkflowsConfigsGlobalWorkflows({
                workflowId: newGlobalId,
                requestBody: newGlobalConfig,
            });
            refreshPromise = () => globalWorkflowsAsync.execute();
        }
        else if ('modules' in newGlobalConfig)
        {
            type = 'step';
            savePromise = StepConfigService.putApiV1WorkflowsConfigsGlobalSteps({
                stepId: newGlobalId,
                requestBody: newGlobalConfig,
            });
            refreshPromise = () => globalStepsAsync.execute();
        }
        else
        {
            type = 'module';
            savePromise = ModuleConfigService.putApiV1WorkflowsConfigsGlobalModules({
                moduleId: newGlobalId,
                requestBody: newGlobalConfig,
            });
            refreshPromise = () => globalModulesAsync.execute();
        }

        // 3. 执行保存和刷新
        try
        {
            await savePromise;
            await refreshPromise();
        } catch (error)
        {
            console.error(`将配置“${newGlobalConfig.name}”保存为全局 ${type} 时失败:`, error);
            throw error;
        }
    }


    /**
     * 新增计算属性，用于判断整个工作台是否存在任何未保存的更改。
     * 这将用于在用户关闭浏览器标签页时发出警告。
     */
    const hasDirtyDrafts = computed(() =>
    {
        return Object.keys(drafts.value).some(globalId => _isDirty(globalId));
    });

    /**
     * 保存所有未保存的更改，并返回详细结果。
     * @returns 返回一个 Promise，该 Promise 解析为一个包含成功和失败列表的对象。
     */
    async function saveAllDirtyDrafts(): Promise<{ saved: SaveResult[], failed: SaveResult[] }>
    {
        const dirtyDrafts = Object.keys(drafts.value).filter(id => _isDirty(id));

        if (dirtyDrafts.length === 0)
        {
            console.log("没有需要保存的更改。");
            return {saved: [], failed: []};
        }

        console.log(`准备保存 ${dirtyDrafts.length} 个已修改的草稿...`);

        const savePromises = dirtyDrafts.map(id =>
        {
            const draft = drafts.value[id];
            return _saveDraft(draft.type, id);
        });

        const results = await Promise.all(savePromises);

        const saved = results.filter(r => r.success);
        const failed = results.filter(r => !r.success);

        console.log("所有更改已成功保存。");
        return {saved, failed};
    }


    /**
     * 获取（申请）一个编辑会话。这是进行任何修改操作的唯一入口。
     * @param type - 要编辑的配置类型
     * @param globalId - 全局配置的ID
     * @returns 如果成功，返回一个 EditSession 实例；如果已被锁定或找不到，返回 null。
     */
    async function acquireEditSession(type: ConfigType, globalId: string): Promise<EditSession | null>
    {

        // 检查是否已存在同名草稿
        if (drafts.value[globalId])
        {
            console.warn(`“${globalId}” 正在被编辑。`);
            // 如果需要，可以返回已存在的会话实例
            return new EditSession(type, globalId);
        }

        // 2. 根据类型，选择对应的异步状态对象
        let stateObject;
        switch (type)
        {
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
        if (!stateObject.isReady)
        {
            console.log(`'${type}' 数据未就绪，开始加载...`);
            // execute() 自身会处理正在加载中的情况，所以直接 await 即可
            await stateObject.execute();
        }

        // 4. 检查加载是否出错
        if (stateObject.error)
        {
            // message.error(`获取 '${type}' 列表时发生错误，无法开始编辑。`);
            console.error(`获取 '${type}' 列表时发生错误：`, stateObject.error);
            return null;
        }

        // 5. 从新的数据结构 (GlobalResourceItem[]) 中查找源数据
        const sourceData = stateObject.state;
        const sourceItem = sourceData[globalId];

        if (!sourceItem)
        {
            console.error(`在 '${type}' 列表中找不到 ID 为 '${globalId}' 的配置项。`);
            return null;
        }

        // 6. 检查找到的项是否已损坏
        if (!sourceItem.isSuccess)
        {
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
        moduleSchemasAsync,
        moduleMetadata,

        hasDirtyDrafts,
        isDirty: _isDirty,

        // --- 核心服务方法 ---
        saveAllDirtyDrafts,
        acquireEditSession,

        // 其他
        createGlobalConfig,

        // --- 内部方法，供 EditSession 使用 ---
        // Vue 3 的 defineStore setup 语法不允许真正意义上的私有化，
        // 我们通过命名约定（下划线），来表示它们是内部API。
        ...internalApi
    };
});
// --- END OF FILE frontend/src/app-workbench/stores/workbenchStore.ts ---