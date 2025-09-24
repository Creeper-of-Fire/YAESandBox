import type {WorkflowConfig} from "#/types/generated/workflow-config-api-client";

/**
 * 工作流配置编辑上下文类型
 * @description 用于在组件间传递工作流配置的核心数据
 */
export interface WorkflowEditorContext {
    /** 当前选中的工作流的唯一全局ID */
    globalId: string;
    /** 当前选中的工作流配置数据 */
    data: WorkflowConfig;
}