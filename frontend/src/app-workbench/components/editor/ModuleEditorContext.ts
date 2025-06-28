import type {AbstractModuleConfig} from "@/app-workbench/types/generated/workflow-config-api-client";

/**
 * 模块配置编辑上下文类型
 * @description 用于在组件间传递模块配置的核心数据，作为模块编辑器的基础上下文包装
 */
export interface ModuleEditorContext {
    /** 当前选中的模块配置数据（核心业务数据） */
    data: AbstractModuleConfig;
}