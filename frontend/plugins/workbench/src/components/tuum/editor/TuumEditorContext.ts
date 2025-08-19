import type {TuumConfig} from "#/types/generated/workflow-config-api-client";

/**
 * 枢机配置编辑上下文类型
 * @description 用于在组件间传递枢机配置的编辑上下文信息，包含核心配置数据及工作流上下文相关信息
 */
export interface TuumEditorContext
{
    /** 当前选中的枢机配置数据（核心业务数据） */
    data: TuumConfig;
}