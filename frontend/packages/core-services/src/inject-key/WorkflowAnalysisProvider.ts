import type {InjectionKey} from "vue";
import type {WorkflowConfig, WorkflowValidationReport} from "../types";

/**
 * @interface IWorkflowAnalysisProvider
 * @description 定义一个提供工作流静态分析报告的契约。
 * 消费方通过此接口按需获取单个工作流的分析结果，接口实现者负责缓存和API调用。
 */
export interface IWorkflowAnalysisProvider
{
    /**
     * @description 按需获取或重新获取单个工作流的分析报告。
     * @param workflowConfig - 要分析的工作流的完整配置对象。
     * @returns {Promise<WorkflowValidationReport | undefined>} 当分析完成时解析的 Promise。
     */
    getReport: (workflowConfig: WorkflowConfig) => Promise<WorkflowValidationReport | undefined>;

    /**
     * @description 清除所有缓存的分析报告。
     */
    clearCache: () => void;
}

/**
 * @description 用于在整个应用中注入 IWorkflowAnalysisProvider 实现的 InjectionKey。
 */
export const WorkflowAnalysisProviderKey: InjectionKey<IWorkflowAnalysisProvider> = Symbol('WorkflowAnalysisProviderKey');