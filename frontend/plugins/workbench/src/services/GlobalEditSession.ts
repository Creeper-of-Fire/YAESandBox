// --- START OF FILE frontend/src/app-workbench/services/GlobalEditSession.ts ---

import {computed, isReactive, isRef, readonly, ref, type Ref} from 'vue';
import type {AbstractRuneConfig, TuumConfig, WorkflowConfig,} from '#/types/generated/workflow-config-api-client';
import {
    type AnyResourceItemSuccess as AnyResourceItemInStore,
    type SaveResult,
    type StoredConfigAddition,
    useWorkbenchStore,
} from '#/stores/workbenchStore.ts';
import {cloneDeep} from "lodash-es";
import {isEquivalent} from "@yaesandbox-frontend/core-services";

// 定义了可编辑配置的类型别名，方便在整个应用中重用。
export type ConfigType = 'workflow' | 'tuum' | 'rune';
export type AnyConfigObject = WorkflowConfig | TuumConfig | AbstractRuneConfig;
export type AnyResourceItemSuccess = AnyResourceItemInStore;

export function getConfigObjectType(config: AnyConfigObject)
{
    if ('workflowInputs' in config && 'tuums' in config)
        return {type: 'workflow' as const, config: config as WorkflowConfig};
    else if ('runes' in config)
        return {type: 'tuum' as const, config: config as TuumConfig};
    else
        return {type: 'rune' as const, config: config as AbstractRuneConfig};
}

function createDebuggableRef(initialValue: any)
{
    let innerValue = initialValue;
    // Vue 的 ref 对象
    const actualRef = ref(initialValue);

    // 用 Proxy 包裹实际的 ref 对象
    return new Proxy(actualRef, {
        get(target, prop, receiver)
        {
            if (prop === 'value')
            {
                // 在访问 .value 时打印信息
                console.groupCollapsed(`DEBUG: Accessing ${prop} of ref (current value: ${target.value})`);
                console.trace('Call stack for get');
                console.groupEnd();
            }
            return Reflect.get(target, prop, receiver);
        },
        set(target, prop, newValue, receiver)
        {
            if (prop === 'value')
            {
                console.groupCollapsed(`DEBUG: Setting ${prop} of ref from ${target.value} to ${newValue}`);
                console.trace('Call stack for set');
                console.groupEnd();

                // 检查 newValue 是否为非响应式对象
                if (typeof newValue === 'object' && newValue !== null && !isRef(newValue) && !isReactive(newValue))
                {
                    console.warn(`Potential issue: ref.value is being set to a non-reactive object.`, newValue);
                }
            }

            // 如果整个 ref 对象被替换，这里也能捕获到
            if (prop === 'value' && target.value === undefined && newValue === undefined)
            {
                console.warn(`DEBUG: Ref might be losing its value reference entirely!`, {target, newValue});
            }

            return Reflect.set(target, prop, newValue, receiver);
        }
    });
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
    public readonly storeId: string; // 源资源项的存储ID，同时也是本配置项的ID
    public isNew: boolean; // 标记这是否是一个全新的、尚未保存的草稿
    public readonly isReadOnly: Ref<boolean>;
    /**
     * @internal - 对 workbenchStore 的引用，用于后端交互
     */
    private readonly _store: ReturnType<typeof useWorkbenchStore>;
    private readonly draftResourceItem: Ref<AnyResourceItemSuccess>;
    private originalState: Ref<string>; // JSON 字符串快照，用于脏检查

    private readonly _version = ref(0);
    public readonly version: Readonly<Ref<number>> = readonly(this._version);


    /**
     * @param type - 配置项类型
     * @param storeId - 源资源的存储ID，同时也是本配置项的ID
     * @param sourceItem -  用于创建会话的完整源资源项 (GlobalResourceItem)
     * @param isNew - 标记是否为新创建的项
     */
    constructor(type: ConfigType, storeId: string, sourceItem: AnyResourceItemSuccess, isNew: boolean = false)
    {
        this._store = useWorkbenchStore();
        this.type = type;
        this.isNew = isNew;

        this.storeId = storeId;

        // 深度克隆源资源项作为草稿的初始状态
        this.draftResourceItem = ref(cloneDeep(sourceItem));
        this.originalState = ref(JSON.stringify(sourceItem));

        this.isReadOnly = computed(() => this.draftResourceItem.value.isReadOnly);
    }

    // --- 公共API (供UI组件使用) ---

    /**
     * 获取当前正在编辑的数据对象。
     * 返回一个响应式的计算属性，当底层Store中的数据变化时，UI会自动更新。
     */
    public getData(): Ref<AnyConfigObject>
    {
        return computed(() => this.draftResourceItem.value.data);
    }

    /**
     * 获取完整的草稿资源项，包括元数据。
     * 用于需要编辑元数据（如描述、标签）或存储相关内容（如 storeRef）的UI。
     */
    public getFullDraft(): Ref<AnyResourceItemSuccess>
    {
        return this.draftResourceItem;
    }

    /**
     * 检查会话是否有未保存的更改。
     */
    public getIsDirty(): Ref<boolean>
    {
        const data = this.draftResourceItem.value;
        return computed(() =>
        {
            if (this.isReadOnly.value)
                return false;

            return !isEquivalent(data, JSON.parse(this.originalState.value));
        });
    }

    /**
     * 更新草稿数据。
     * @param updatedData - 包含部分或全部更新字段的对象。
     */
    public updateData(updatedData: Partial<AnyConfigObject>): void
    {
        if (this.isReadOnly.value)
            return;

        // 使用 Object.assign 来合并更新，确保 Ref 的响应性
        Object.assign(this.draftResourceItem.value.data, updatedData);
    }

    /**
     * 更新资源项的根级属性 (如 storeRef, meta)。
     * @param updatedProperties - 包含要更新的根属性的对象。
     */
    public updateRootProperties(updatedProperties: Partial<StoredConfigAddition>): void
    {
        Object.assign(this.draftResourceItem.value, updatedProperties);
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
     * 保存当前会话的更改。
     * 它将调用 store 提供的后端服务接口。
     */
    public async save(): Promise<SaveResult>
    {
        if (this.isReadOnly.value)
            return {
                success: false,
                storeId: this.storeId,
                name: this.draftResourceItem.value.data.name,
                type: this.type,
                error: '本内容是只读的。'
            };

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
        this.draftResourceItem.value = JSON.parse(this.originalState.value);
        this._version.value++;
    }

    /**
     * @description 提交更改，将当前草稿状态设为新的基准线。
     *  这个方法应该在后端成功保存后被调用。
     */
    private commitChanges(): void
    {
        // 更新原始状态快照为当前草稿的状态
        this.originalState.value = JSON.stringify(this.draftResourceItem.value);
        // 如果这是一个新创建的会话，那么在第一次成功保存后，它就不再是“新的”了
        if (this.isNew)
        {
            this.isNew = false;
        }
        this._version.value++;
    }
}