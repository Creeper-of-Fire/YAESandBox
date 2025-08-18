import type {AbstractRuneConfig} from "@/types/generated/workflow-config-api-client";

/**
 * 符文配置编辑上下文类型
 * @description 用于在组件间传递符文配置的核心数据，作为符文编辑器的基础上下文包装
 */
export interface RuneEditorContext {
    /** 当前选中的符文配置数据（核心业务数据） */
    data: AbstractRuneConfig;
}