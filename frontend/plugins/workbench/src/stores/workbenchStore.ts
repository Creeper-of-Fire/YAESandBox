// --- START OF FILE frontend/src/app-workbench/stores/workbenchStore.ts ---

import {defineStore} from 'pinia';
import {computed, type Reactive, reactive, ref} from 'vue';
import {v4 as uuidv4} from 'uuid';
import {type AnyConfigObject, type ConfigType, GlobalEditSession, getConfigObjectType,} from '#/services/GlobalEditSession.ts';
import type {AbstractRuneConfig, RuneSchemasResponse, TuumConfig, WorkflowConfig,} from "#/types/generated/workflow-config-api-client";
import {RuneConfigService, TuumConfigService, WorkflowConfigService,} from "#/types/generated/workflow-config-api-client";
import {useAsyncState, type UseAsyncStateReturn} from "@vueuse/core";
import type {GlobalResourceItem} from "@yaesandbox-frontend/core-services/types";
import {type DynamicAsset, loadAndRegisterPlugins} from "#/features/schema-viewer/plugin-loader";

// 导出类型
export type WorkbenchStore = ReturnType<typeof useWorkbenchStore>;

// 为不同类型的资源创建具体的别名，方便使用
export type WorkflowResourceItem = GlobalResourceItem<WorkflowConfig>;
export type TuumResourceItem = GlobalResourceItem<TuumConfig>;
export type RuneResourceItem = GlobalResourceItem<AbstractRuneConfig>;

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

export interface WorkflowRuneRules
{
    noConfig?: boolean;
    singleInTuum?: boolean;
    inFrontOf?: string[]; // 存储的是符文的类型名 (e.g., "PromptGenerationRuneConfig")
    behind?: string[]; // 存储的是符文的类型名 (e.g., "PromptGenerationRuneConfig")
}

/**
 * 元数据接口，整合了规则和类别标签。
 * 这提供了一个统一的元数据访问点。
 */
export interface RuneMetadata
{
    rules?: WorkflowRuneRules;
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
    data: AnyConfigObject;
    originalState: string;
}

export const useWorkbenchStore = defineStore('workbench', () =>
{
    // =================================================================
    // 资源处理器注册表 (Resource Handler Registry)
    // =================================================================

    /**
     * @description 定义一个通用的资源处理器接口，用于统一不同资源类型的操作。
     */
    interface IResourceHandler<T extends AnyConfigObject>
    {
        // 每种资源的异步状态管理
        asyncState: Reactive<UseAsyncStateReturn<Record<string, GlobalResourceItem<T>>, any[], true>>;
        // API 调用函数
        api: {
            // 保存或更新资源
            save: (id: string, requestBody: T) => Promise<any>;
            // 删除资源
            delete: (id: string) => Promise<any>;
        };
        // API 调用时，ID参数的名称 (e.g., 'workflowId')
        idParamName: string;
    }

    /**
     * @description 资源处理器注册表。
     * 这是本次优化的核心。它将每种资源类型的特定实现（API调用、状态管理）集中配置，
     * 使得后续的操作函数可以变得通用，从而消除重复的 switch-case 逻辑。
     * 如果未来要增加新的资源类型（例如 'plugin'），只需在此处添加一个新的条目即可。
     */
    const resourceHandlers: { [K in ConfigType]: IResourceHandler<any> } = {
        workflow: {
            asyncState: reactive(useAsyncState(
                () => WorkflowConfigService.getApiV1WorkflowsConfigsGlobalWorkflows().then(processDtoToViewModel),
                {} as Record<string, WorkflowResourceItem>,
                {immediate: false, shallow: false}
            )),
            api: {
                save: (workflowId, requestBody) => WorkflowConfigService.putApiV1WorkflowsConfigsGlobalWorkflows({workflowId, requestBody}),
                delete: (workflowId) => WorkflowConfigService.deleteApiV1WorkflowsConfigsGlobalWorkflows({workflowId}),
            },
            idParamName: 'workflowId',
        },
        tuum: {
            asyncState: reactive(useAsyncState(
                () => TuumConfigService.getApiV1WorkflowsConfigsGlobalTuums().then(processDtoToViewModel),
                {} as Record<string, TuumResourceItem>,
                {immediate: false, shallow: false}
            )),
            api: {
                save: (tuumId, requestBody) => TuumConfigService.putApiV1WorkflowsConfigsGlobalTuums({tuumId, requestBody}),
                delete: (tuumId) => TuumConfigService.deleteApiV1WorkflowsConfigsGlobalTuums({tuumId}),
            },
            idParamName: 'tuumId',
        },
        rune: {
            asyncState: reactive(useAsyncState(
                () => RuneConfigService.getApiV1WorkflowsConfigsGlobalRunes().then(processDtoToViewModel),
                {} as Record<string, RuneResourceItem>,
                {immediate: false, shallow: false}
            )),
            api: {
                save: (runeId, requestBody) => RuneConfigService.putApiV1WorkflowsConfigsGlobalRunes({runeId, requestBody}),
                delete: (runeId) => RuneConfigService.deleteApiV1WorkflowsConfigsGlobalRunes({runeId}),
            },
            idParamName: 'runeId',
        },
    };

    // =================================================================
    // 内部 State (完全封装)
    // =================================================================

    const globalWorkflowsAsync = computed(() => resourceHandlers.workflow.asyncState);
    const globalTuumsAsync = computed(() => resourceHandlers.tuum.asyncState);
    const globalRunesAsync = computed(() => resourceHandlers.rune.asyncState);

    /**
     * 存储所有符文类型的 Schema
     * Key 是符文的 runeType, Value 是对应的 JSON Schema
     */
    const runeSchemasAsync = reactive(useAsyncState(
        async () =>
        {
            console.log("正在获取符文Schema和动态资源...");
            const response: RuneSchemasResponse = await RuneConfigService.getApiV1WorkflowsConfigsGlobalRunesAllRuneConfigsSchemas();

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
     * 它会从每个符文的 Schema 中提取 `x-workflow-rune-rules` 和 `x-classLabel` 等元数据信息。
     */
    const runeMetadata = computed(() =>
    {
        const metadataMap: Record<string, RuneMetadata> = {};
        const schemas = runeSchemasAsync.state;

        if (schemas)
        {
            for (const runeType in schemas)
            {
                const schema = schemas[runeType];
                if (schema)
                {
                    const rules = schema['x-workflow-rune-rules'] as WorkflowRuneRules | undefined;
                    // 从 Schema 中提取新的类别标签属性
                    const classLabel = schema['x-classLabel'] as string | undefined;

                    // 只要 Schema 中包含任何一个元数据，就为其创建一个条目
                    if (rules || classLabel)
                    {
                        const metadata: RuneMetadata = {};
                        if (rules)
                        {
                            metadata.rules = rules;
                        }
                        if (classLabel)
                        {
                            metadata.classLabel = classLabel;
                        }
                        metadataMap[runeType] = metadata;
                    }
                }
            }
        }
        return metadataMap;
    });

    /**
     * @description 存储所有活跃的 GlobalEditSession 实例。
     * Key 是 session.globalId。
     */
    const activeSessions = ref<Record<string, GlobalEditSession>>({});

    // =================================================================
    // 内部 Getter & Action (加下划线表示，约定不对外暴露)
    // =================================================================

    /**
     * @description 计算是否存在任何一个变脏的会话。
     */
    const hasDirtyDrafts = computed(() =>
    {
        return Object.values(activeSessions.value).some(session => session.getIsDirty().value);
    });

    // TODO 没写好校验逻辑
    // _saveDraft 现在接收 globalId
    const saveSessionData = async (session: GlobalEditSession): Promise<SaveResult> =>
    {
        const {type, globalId} = session;
        const draftData = session.getData().value;
        const name = draftData.name;

        const handler = resourceHandlers[type];
        if (!handler)
        {
            const error = `保存失败：未知的草稿类型 '${type}'。`;
            console.error(error);
            return {success: false, name, id: globalId, type, error};
        }

        try
        {
            await handler.api.save(globalId, draftData);
            await handler.asyncState.execute();
            return {success: true, id: globalId, name, type};
        } catch (error)
        {
            console.error(`保存 ${type} 草稿到后端时发生错误:`, error);
            return {success: false, id: globalId, name, type, error};
        }
    };

    // =================================================================
    // 公共 API (暴露给外部世界的精简接口)
    // =================================================================

    /**
     * @description 从后端和本地状态中删除一个全局配置项。
     * @param type - 要删除的配置类型
     * @param id - 全局配置的ID
     * @returns 如果成功，返回 true；否则返回 false。
     */
    async function deleteGlobalConfig(type: ConfigType, id: string): Promise<boolean>
    {
        const handler = resourceHandlers[type];
        if (!handler)
        {
            console.error(`删除失败：未知的配置类型 '${type}'。`);
            return false;
        }

        try
        {
            await handler.api.delete(id);
            if (handler.asyncState.state[id])
            {
                delete handler.asyncState.state[id];
            }
            closeSession(id)
            return true;
        } catch (error)
        {
            console.error(`删除 ${type} (ID: ${id}) 时发生错误:`, error);
            // 这里可以向上抛出错误，让调用方处理 message 提示
            throw error;
        }
    }

    /**
     * 将一个已有的配置对象保存为新的全局配置。
     * @param configToSave - 要保存为全局的配置对象。
     */
    async function createGlobalConfig(configToSave: AnyConfigObject)
    {
        // 深克隆并确保ID是全新的（即使是克隆来的）
        const newGlobalConfig = deepCloneWithNewIds(configToSave);
        const newGlobalId = uuidv4(); // 为这个新的全局资源创建一个全新的顶级ID

        const type: ConfigType = getConfigObjectType(newGlobalConfig);
        const handler = resourceHandlers[type];

        // 执行保存和刷新
        try
        {
            await handler.api.save(newGlobalId, newGlobalConfig);
            await handler.asyncState.execute();
        } catch (error)
        {
            console.error(`将配置“${newGlobalConfig.name}”保存为全局 ${type} 时失败:`, error);
            throw error;
        }
    }

    /**
     * 获取（申请）一个编辑会话。这是进行任何修改操作的唯一入口。
     * @param type - 要编辑的配置类型
     * @param globalId - 全局配置的ID
     * @returns 如果成功，返回一个 GlobalEditSession 实例；如果已被锁定或找不到，返回 null。
     */
    async function acquireEditSession(type: ConfigType, globalId: string): Promise<GlobalEditSession | null>
    {

        // 1. 如果已存在此会话，直接返回
        if (activeSessions.value[globalId])
        {
            return activeSessions.value[globalId];
        }

        // 2. 根据类型，选择对应的异步状态对象
        const handler = resourceHandlers[type];
        if (!handler)
        {
            console.error(`未知的配置类型: ${type}`);
            return null;
        }

        const stateObject = handler.asyncState;

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

        // 创建新的 GlobalEditSession 实例
        const session = new GlobalEditSession(type, globalId, sourceItem.data, false);

        // 将新会话存入 activeSessions
        activeSessions.value[session.globalId] = session;

        return session;
    }

    /**
     * @description 为全新的、未保存的配置项创建一个编辑会话。
     * @param type - 资源类型
     * @param blankConfig - 一个空白的配置对象
     */
    function createNewDraftSession(type: ConfigType, blankConfig: AnyConfigObject): GlobalEditSession
    {
        const newGlobalId = uuidv4();

        // 1. 直接用空白配置创建一个新的 GlobalEditSession 实例
        const session = new GlobalEditSession(type, newGlobalId, blankConfig, true);

        // 2. 将其添加到活跃会话中
        activeSessions.value[session.globalId] = session;

        return session;
    }

    /**
     * @description 关闭一个会话，并从活跃列表中移除。
     */
    function closeSession(sessionID: string)
    {
        if (sessionID && activeSessions.value[sessionID])
        {
            delete activeSessions.value[sessionID];
        }
    }

    async function saveAllDirtyDrafts(): Promise<{ saved: SaveResult[], failed: SaveResult[] }>
    {
        const dirtySessions = Object.values(activeSessions.value).filter(s => s.getIsDirty().value);

        if (dirtySessions.length === 0)
        {
            return {saved: [], failed: []};
        }

        const savePromises = dirtySessions.map(session => session.save());
        const results = await Promise.all(savePromises);

        const saved = results.filter(r => r.success);
        const failed = results.filter(r => !r.success);

        return {saved, failed};
    }


    // 我们需要把所有 GlobalEditSession 需要的“私有”方法也 return 出去，
    // 这样注入的 storeInstance 才拥有这些方法。
    const internalApi = {
        saveSessionData
    };

    return {
        // --- 只读数据访问器 ---
        globalWorkflowsAsync,
        globalTuumsAsync,
        globalRunesAsync,
        runeSchemasAsync,
        runeMetadata,

        hasDirtyDrafts,

        // --- 核心服务方法 ---
        acquireEditSession,
        createNewDraftSession,
        saveAllDirtyDrafts,
        closeSession,

        // 其他
        createGlobalConfig,
        deleteGlobalConfig,

        // --- 内部方法，供 GlobalEditSession 使用 ---
        // Vue 3 的 defineStore setup 语法不允许真正意义上的私有化，
        // 我们通过命名约定（下划线），来表示它们是内部API。
        ...internalApi
    };
});
// --- END OF FILE frontend/src/app-workbench/stores/workbenchStore.ts ---