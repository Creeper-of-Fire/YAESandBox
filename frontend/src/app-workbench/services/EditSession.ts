// --- START OF FILE frontend/src/app-workbench/services/EditSession.ts ---

import {computed, type Ref} from 'vue';
import type {
    AbstractRuneConfig,
    TuumConfig,
    WorkflowConfig,
} from '@/app-workbench/types/generated/workflow-config-api-client';
import {type SaveResult, useWorkbenchStore} from '@/app-workbench/stores/workbenchStore.ts';

// 定义了可编辑配置的类型别名，方便在整个应用中重用。
export type ConfigType = 'workflow' | 'tuum' | 'rune';
export type ConfigObject = WorkflowConfig | TuumConfig | AbstractRuneConfig;

// /**
//  * EditSession 的公共接口 (契约)。
//  * 组件应该依赖这个接口，而不是具体的 EditSession 类。
//  * 这解决了类实例与其响应式代理之间的类型兼容性问题。
//  */
// export interface IEditSession {
//     readonly type: ConfigType;
//     readonly globalId: string;
//     readonly globalId: string;
//
//     // 注意：这里的 Ref<T> 类型与 computed 的返回类型完全匹配
//     readonly data: Ref<ConfigObject | null>;
//     readonly isDirty: Ref<boolean>;
//
//     updateData(updatedData: Partial<ConfigObject>): void;
//     save(): Promise<void>;
//     close(): boolean;
//
//     // UI 状态管理
//     getSelectedItemId(): Ref<string | null>;
//     selectItem(itemId: string | null): void;
//     isTuumExpanded(tuumId: string): Ref<boolean>;
//     toggleTuumExpansion(tuumId: string): void;
// }

/**
 * 编辑会话句柄 (The "Little Package")
 * 这是一个代表已授权编辑会话的轻量级对象。
 * 它由 `useWorkbenchStore` 创建和管理，并作为与UI层交互的唯一“契约”。
 * UI组件通过这个对象与底层Store安全地交互，而无需知道Store的内部实现。
 */
export class EditSession
{
    /**
     * @internal - 对 workbenchStore 的引用
     */
    _getStore()
    {
        return useWorkbenchStore();
    }

    public readonly type: ConfigType;
    public readonly globalId: string; // 原始全局ID，用于识别锁定和可能的保存目标

    /**
     * @internal - 这个构造函数只应该被 workbenchStore 调用。
     * 我们用 @internal JSDoc 标签来标记它，IDE会给出提示，表示它不应被外部直接调用。
     * 这是实现“友元类”效果的一种约定。
     * @param type - 配置项类型
     * @param globalId - 此会话对应的原始全局配置ID
     */
    constructor(type: ConfigType, globalId: string)
    {
        this.type = type;
        this.globalId = globalId;
    }

    // --- 公共API (供UI组件使用) ---

    /**
     * 获取当前正在编辑的数据对象。
     * 返回一个响应式的计算属性，当底层Store中的数据变化时，UI会自动更新。
     */
    public getData(): Ref<ConfigObject | null>
    {
        return computed(() => this._getStore()._getDraftData(this.globalId));
    }

    /**
     * 检查会话是否有未保存的更改。
     * 返回一个响应式的计算属性。
     */
    public getIsDirty(): Ref<boolean>
    {
        return computed(() => this._getStore()._isDirty(this.globalId));
    }

    /**
     * 更新草稿数据。UI组件中的表单修改会调用此方法。
     * @param updatedData - 包含部分或全部更新字段的对象。
     */
    public updateData(updatedData: Partial<ConfigObject>): void
    {
        this._getStore()._updateDraftData(this.globalId, updatedData);
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
     * 保存当前会话的更改到后端。
     */
    public async save(): Promise<SaveResult>
    {
        return await this._getStore()._saveDraft(this.type, this.globalId);
    }

    /**
     * close() 重命名为 discard()，并且不再返回布尔值。
     * 调用此方法会无条件地丢弃当前会话的草稿。
     * 这应该只在用户明确想要“撤销所有更改”时使用。
     */
    public discard(): void
    {
        this._getStore()._discardDraft(this.globalId);
    }
}

// --- END OF FILE frontend/src/app-workbench/services/EditSession.ts ---