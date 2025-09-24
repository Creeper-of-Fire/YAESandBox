// --- START OF FILE frontend/src/app-workbench/services/GlobalEditSession.ts ---

import {computed, ref, type Ref} from 'vue';
import type {AbstractRuneConfig, TuumConfig, WorkflowConfig,} from '#/types/generated/workflow-config-api-client';
import {type SaveResult, useWorkbenchStore} from '#/stores/workbenchStore.ts';
import {cloneDeep} from "lodash-es";
import {isEquivalent} from "@yaesandbox-frontend/core-services";

// 定义了可编辑配置的类型别名，方便在整个应用中重用。
export type ConfigType = 'workflow' | 'tuum' | 'rune';
export type AnyConfigObject = WorkflowConfig | TuumConfig | AbstractRuneConfig;

export function getConfigObjectType(obj: AnyConfigObject): ConfigType
{
    let type;
    if ('workflowInputs' in obj && 'tuums' in obj)
        type = 'workflow' as const;
    else if ('runes' in obj)
        type = 'tuum' as const;
    else
        type = 'rune' as const;
    return type
}

/**
 * 编辑会话句柄 (The "Little Package")
 * 这是一个代表已授权编辑会话的轻量级对象。
 * 它由 `useWorkbenchStore` 创建和管理，并作为与UI层交互的唯一“契约”。
 * UI组件通过这个对象与底层Store安全地交互，而无需知道Store的内部实现。
 */
export class GlobalEditSession
{
    public readonly type: ConfigType;
    public readonly globalId: string; // 源数据的全局ID，同时也是本配置项的ID
    public isNew: boolean; // 标记这是否是一个全新的、尚未保存的草稿
    /**
     * @internal - 对 workbenchStore 的引用，用于后端交互
     */
    private readonly _store: ReturnType<typeof useWorkbenchStore>;
    private readonly draftData: Ref<AnyConfigObject>;
    private originalState: string; // JSON 字符串快照，用于脏检查


    /**
     * @param type - 配置项类型
     * @param globalId - 源数据的全局ID，同时也是本配置项的ID
     * @param sourceData - 用于创建会话的源数据对象
     * @param isNew - 标记是否为新创建的项
     */
    constructor(type: ConfigType, globalId: string, sourceData: AnyConfigObject, isNew: boolean = false)
    {
        this._store = useWorkbenchStore();
        this.type = type;
        this.isNew = isNew;

        this.globalId = globalId;

        // 深度克隆源数据作为草稿的初始状态
        this.draftData = ref(cloneDeep(sourceData));
        this.originalState = JSON.stringify(sourceData);
    }

    // --- 公共API (供UI组件使用) ---

    /**
     * 获取当前正在编辑的数据对象。
     * 返回一个响应式的计算属性，当底层Store中的数据变化时，UI会自动更新。
     */
    public getData(): Ref<AnyConfigObject>
    {
        return this.draftData;
    }

    /**
     * 检查会话是否有未保存的更改。
     */
    public getIsDirty(): Ref<boolean>
    {
        return computed(() => !isEquivalent(this.draftData.value, JSON.parse(this.originalState)));
    }

    /**
     * 更新草稿数据。
     * @param updatedData - 包含部分或全部更新字段的对象。
     */
    public updateData(updatedData: Partial<AnyConfigObject>): void
    {
        // 使用 Object.assign 来合并更新，确保 Ref 的响应性
        Object.assign(this.draftData.value, updatedData);
    }

    /**
     * *** 重命名当前编辑项 ***
     * 这是一个便捷方法，用于更新配置对象的 name 属性。
     * @param newName - 新的名称
     */
    public rename(newName: string): void
    {
        this.updateData({name: newName} as Partial<AnyConfigObject>);
    }

    /**
     * @description 提交更改，将当前草稿状态设为新的基准线。
     *  这个方法应该在后端成功保存后被调用。
     */
    public commitChanges(): void
    {
        // 更新原始状态快照为当前草稿的状态
        this.originalState = JSON.stringify(this.draftData.value);
        // 如果这是一个新创建的会话，那么在第一次成功保存后，它就不再是“新的”了
        if (this.isNew)
        {
            this.isNew = false;
        }
    }

    /**
     * 保存当前会话的更改。
     * 它将调用 store 提供的后端服务接口。
     */
    public async save(): Promise<SaveResult>
    {
        // 调用 store 的统一保存方法，将自身和当前草稿数据传递过去
        const result = await this._store.saveSessionData(this);

        if (result.success)
        {
            // 如果后端保存成功，就调用 commitChanges 来更新本会话的状态
            this.commitChanges();
        }

        return result;
    }

    /**
     * 放弃所有未保存的更改，将草稿数据恢复到原始状态。
     */
    public discard(): void
    {
        this.draftData.value = JSON.parse(this.originalState);
    }
}