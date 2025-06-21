// --- START OF FILE frontend/src/app-workbench/services/EditSession.ts ---

import {computed, type Ref} from 'vue';
import type {
    WorkflowProcessorConfig,
    StepProcessorConfig,
    AbstractModuleConfig,
} from '@/app-workbench/types/generated/workflow-config-api-client';
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore.ts';

// 定义了可编辑配置的类型别名，方便在整个应用中重用。
export type ConfigType = 'workflow' | 'step' | 'module';
export type ConfigObject = WorkflowProcessorConfig | StepProcessorConfig | AbstractModuleConfig;

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
//     isStepExpanded(stepId: string): Ref<boolean>;
//     toggleStepExpansion(stepId: string): void;
// }

/**
 * 编辑会话句柄 (The "Little Package")
 * 这是一个代表已授权编辑会话的轻量级对象。
 * 它由 `useWorkbenchStore` 创建和管理，并作为与UI层交互的唯一“契约”。
 * UI组件通过这个对象与底层Store安全地交互，而无需知道Store的内部实现。
 */
export class EditSession {
    /**
     * @internal - 对 workbenchStore 的引用
     */
    _getStore() {
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
    constructor(type: ConfigType, globalId: string) {
        this.type = type;
        this.globalId = globalId;
    }

    // --- 公共API (供UI组件使用) ---

    /**
     * 获取当前正在编辑的数据对象。
     * 返回一个响应式的计算属性，当底层Store中的数据变化时，UI会自动更新。
     */
    public getData(): Ref<ConfigObject | null> {
        return computed(() => this._getStore()._getDraftData(this.globalId));
    }

    /**
     * 检查会话是否有未保存的更改。
     * 返回一个响应式的计算属性。
     */
    public getIsDirty(): Ref<boolean> {
        return computed(() => this._getStore()._isDirty(this.globalId));
    }

    /**
     * 更新草稿数据。UI组件中的表单修改会调用此方法。
     * @param updatedData - 包含部分或全部更新字段的对象。
     */
    public updateData(updatedData: Partial<ConfigObject>): void {
        this._getStore()._updateDraftData(this.globalId, updatedData);
    }

    /**
     * 保存当前会话的更改到后端。
     */
    public async save(): Promise<void> {
        await this._getStore()._saveDraft(this.type, this.globalId);
    }

    /**
     * close() 重命名为 discard()，并且不再返回布尔值。
     * 调用此方法会无条件地丢弃当前会话的草稿。
     * 这应该只在用户明确想要“撤销所有更改”时使用。
     */
    public discard(): void {
        this._getStore()._discardDraft(this.globalId);
    }

    // --- 数据操作 API (拖拽等) ---

    /**
     * 在工作流的指定位置添加一个新步骤。
     * @param stepConfig - 要添加的步骤配置对象（通常来自全局资源的克隆）
     * @param index - 要插入的目标索引
     */
    public addStep(stepConfig: StepProcessorConfig, index: number): void {
        this._getStore()._addStepToDraft(this.globalId, stepConfig, index);
    }

    /**
     * 移动工作流中的步骤。
     * @param fromIndex - 原始索引
     * @param toIndex - 目标索引
     */
    public moveStep(fromIndex: number, toIndex: number): void {
        this._getStore()._moveStepInDraft(this.globalId, fromIndex, toIndex);
    }

    /**
     * 向指定步骤中添加一个新模块。
     * @param moduleConfig - 要添加的模块配置对象（来自全局资源的克隆）
     * @param stepId - 目标步骤的 configId
     * @param index - 在模块列表中的目标索引
     */
    public addModuleToStep(moduleConfig: AbstractModuleConfig, stepId: string, index: number): void {
        this._getStore()._addModuleToDraft(this.globalId, moduleConfig, stepId, index);
    }

    /**
     * 在步骤之间或步骤内部移动模块。
     * @param fromStepId - 模块所在的原始步骤ID
     * @param fromIndex - 模块在原始步骤中的索引
     * @param toStepId - 模块要移动到的目标步骤ID
     * @param toIndex - 模块在目标步骤中的索引
     */
    public moveModule(fromStepId: string, fromIndex: number, toStepId: string, toIndex: number): void {
        this._getStore()._moveModuleInDraft(this.globalId, fromStepId, fromIndex, toStepId, toIndex);
    }
}

// --- END OF FILE frontend/src/app-workbench/services/EditSession.ts ---