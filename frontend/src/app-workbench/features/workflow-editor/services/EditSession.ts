// --- START OF FILE frontend/src/app-workbench/features/workflow-editor/services/EditSession.ts ---

import {computed, type Ref} from 'vue';
import type {
    WorkflowProcessorConfig,
    StepProcessorConfig,
    AbstractModuleConfig,
} from '@/app-workbench/types/generated/workflow-config-api-client';
import {useWorkbenchStore} from '@/app-workbench/features/workflow-editor/stores/workbenchStore';

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
//     readonly draftId: string;
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
    public readonly draftId: string;
    public readonly globalId: string; // 原始全局ID，用于识别锁定和可能的保存目标

    /**
     * @internal - 这个构造函数只应该被 workbenchStore 调用。
     * 我们用 @internal JSDoc 标签来标记它，IDE会给出提示，表示它不应被外部直接调用。
     * 这是实现“友元类”效果的一种约定。
     * @param type - 配置项类型
     * @param draftId - 此会话在Store中对应的草稿ID
     * @param globalId - 此会话对应的原始全局配置ID
     */
    constructor(type: ConfigType, draftId: string, globalId: string) {
        this.type = type;
        this.draftId = draftId;
        this.globalId = globalId;
    }

    // --- 公共API (供UI组件使用) ---

    /**
     * 获取当前正在编辑的数据对象。
     * 返回一个响应式的计算属性，当底层Store中的数据变化时，UI会自动更新。
     */
    public getData(): Ref<ConfigObject | null> {
        return computed(() => this._getStore()._getDraftData(this.draftId));
    }

    /**
     * 检查会话是否有未保存的更改。
     * 返回一个响应式的计算属性。
     */
    public getIsDirty(): Ref<boolean> {
        return computed(() => this._getStore()._isDirty(this.type, this.draftId));
    }

    /**
     * 更新草稿数据。UI组件中的表单修改会调用此方法。
     * @param updatedData - 包含部分或全部更新字段的对象。
     */
    public updateData(updatedData: Partial<ConfigObject>): void {
        this._getStore()._updateDraftData(this.draftId, updatedData);
    }

    /**
     * 保存当前会话的更改到后端。
     */
    public async save(): Promise<void> {
        await this._getStore()._saveDraft(this.type, this.draftId, this.globalId);
    }

    /**
     * 关闭并释放此编辑会话。
     * @returns 是否成功关闭 (如果用户在 'isDirty' 提示时取消，则返回false)
     */
    public close(): boolean {
        return this._getStore()._closeDraft(this.type, this.draftId, this.globalId);
    }

    // --- UI状态管理 ---

    /**
     * 获取当前会话中被选中的项的ID (例如，一个模块的configId)。
     * 返回一个响应式的计算属性。
     */
    public getSelectedItemId(): Ref<string | null> {
        return computed(() => this._getStore()._getUiState(this.draftId)?.selectedItemId ?? null);
    }

    /**
     * 在当前会话中设置选中的项。
     * @param itemId - 要选中的项的ID，或 null 取消选择。
     */
    public selectItem(itemId: string | null): void {
        this._getStore()._setSelectedItemInDraft(this.draftId, itemId);
    }

    /**
     * 检查某个步骤在当前会话中是否处于展开状态。
     * @param stepId - 要检查的步骤的configId。
     * @returns 一个响应式的布尔值计算属性。
     */
    public isStepExpanded(stepId: string): Ref<boolean> {
        return computed(() => this._getStore()._getUiState(this.draftId)?.expandedStepIds.includes(stepId) ?? false);
    }

    /**
     * 切换某个步骤在当前会话中的展开/折叠状态。
     * @param stepId - 要操作的步骤的configId。
     */
    public toggleStepExpansion(stepId: string): void {
        this._getStore()._toggleStepExpansionInDraft(this.draftId, stepId);
    }

    /**
     * 在工作流的指定位置添加一个新步骤。
     * @param stepConfig - 要添加的步骤配置对象（通常来自全局资源的克隆）
     * @param index - 要插入的目标索引
     */
    public addStep(stepConfig: StepProcessorConfig, index: number): void {
        this._getStore()._addStepToDraft(this.draftId, stepConfig, index);
    }

    /**
     * 移动工作流中的步骤。
     * @param fromIndex - 原始索引
     * @param toIndex - 目标索引
     */
    public moveStep(fromIndex: number, toIndex: number): void {
        this._getStore()._moveStepInDraft(this.draftId, fromIndex, toIndex);
    }

    /**
     * 向指定步骤中添加一个新模块。
     * @param moduleConfig - 要添加的模块配置对象（来自全局资源的克隆）
     * @param stepId - 目标步骤的 configId
     * @param index - 在模块列表中的目标索引
     */
    public addModuleToStep(moduleConfig: AbstractModuleConfig, stepId: string, index: number): void {
        this._getStore()._addModuleToDraft(this.draftId, moduleConfig, stepId, index);
    }

    /**
     * 在步骤之间或步骤内部移动模块。
     * @param fromStepId - 模块所在的原始步骤ID
     * @param fromIndex - 模块在原始步骤中的索引
     * @param toStepId - 模块要移动到的目标步骤ID
     * @param toIndex - 模块在目标步骤中的索引
     */
    public moveModule(fromStepId: string, fromIndex: number, toStepId: string, toIndex: number): void {
        this._getStore()._moveModuleInDraft(this.draftId, fromStepId, fromIndex, toStepId, toIndex);
    }
}

// --- END OF FILE frontend/src/app-workbench/features/workflow-editor/services/EditSession.ts ---