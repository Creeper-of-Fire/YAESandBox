// --- START OF FILE frontend/src/app-workbench/services/EditSession.ts ---

import {computed,ref, type Ref} from 'vue';
import type {
    AbstractRuneConfig,
    TuumConfig,
    WorkflowConfig,
} from '#/types/generated/workflow-config-api-client';
import {type SaveResult, useWorkbenchStore} from '#/stores/workbenchStore.ts';
import {v4 as uuidv4} from "uuid";
import {cloneDeep} from "lodash-es";
import {isEquivalent} from "@yaesandbox-frontend/core-services";

// 定义了可编辑配置的类型别名，方便在整个应用中重用。
export type ConfigType = 'workflow' | 'tuum' | 'rune';
export type ConfigObject = WorkflowConfig | TuumConfig | AbstractRuneConfig;

export function getConfigObjectType(obj: ConfigObject): ConfigType
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
export class EditSession
{
    /**
     * @internal - 对 workbenchStore 的引用，用于后端交互
     */
    private readonly _store: ReturnType<typeof useWorkbenchStore>;

    public readonly type: ConfigType;
    public readonly globalId: string; // 原始全局ID，用于识别锁定和可能的保存目标
    public isNew: boolean; // 标记这是否是一个全新的、尚未保存的草稿

    private readonly draftData: Ref<ConfigObject>;
    private originalState: string; // JSON 字符串快照，用于脏检查


    /**
     * @param type - 配置项类型
     * @param sourceData - 用于创建会话的源数据对象
     * @param isNew - 标记是否为新创建的项
     */
    constructor(type: ConfigType, sourceData: ConfigObject, isNew: boolean = false) {
        this._store = useWorkbenchStore();
        this.type = type;
        this.isNew = isNew;

        // 为新会话分配一个唯一的ID
        // 如果是编辑现有项，globalId 就是其持久化ID
        // 如果是新建项，我们给一个临时的UUID
        this.globalId = ('configId' in sourceData && sourceData.configId)
            ? sourceData.configId
            : (isNew ? uuidv4() : (sourceData as WorkflowConfig).name); // WorkflowConfig 没有 configId，用 name

        // 深度克隆源数据作为草稿的初始状态
        this.draftData = ref(cloneDeep(sourceData));
        this.originalState = JSON.stringify(sourceData);
    }

    // --- 公共API (供UI组件使用) ---

    /**
     * 获取当前正在编辑的数据对象。
     * 返回一个响应式的计算属性，当底层Store中的数据变化时，UI会自动更新。
     */
    public getData(): Ref<ConfigObject>
    {
        return this.draftData;
    }

    /**
     * 检查会话是否有未保存的更改。
     */
    public getIsDirty(): Ref<boolean> {
        return computed(() => !isEquivalent(this.draftData.value, JSON.parse(this.originalState)));
    }

    /**
     * 更新草稿数据。
     * @param updatedData - 包含部分或全部更新字段的对象。
     */
    public updateData(updatedData: Partial<ConfigObject>): void {
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
        this.updateData({name: newName} as Partial<ConfigObject>);
    }

    /**
     * @description 提交更改，将当前草稿状态设为新的基准线。
     *  这个方法应该在后端成功保存后被调用。
     */
    public commitChanges(): void {
        // 更新原始状态快照为当前草稿的状态
        this.originalState = JSON.stringify(this.draftData.value);
        // 如果这是一个新创建的会话，那么在第一次成功保存后，它就不再是“新的”了
        if (this.isNew) {
            this.isNew = false;
        }
    }

    /**
     * 保存当前会话的更改。
     * 它将调用 store 提供的后端服务接口。
     */
    public async save(): Promise<SaveResult> {
        // 调用 store 的统一保存方法，将自身和当前草稿数据传递过去
        const result = await this._store.saveSessionData(this);

        if (result.success) {
            // 如果后端保存成功，就调用 commitChanges 来更新本会话的状态
            this.commitChanges();
        }

        return result;
    }

    /**
     * 放弃所有未保存的更改，将草稿数据恢复到原始状态。
     */
    public discard(): void {
        this.draftData.value = JSON.parse(this.originalState);
    }
}

// --- END OF FILE frontend/src/app-workbench/services/EditSession.ts ---