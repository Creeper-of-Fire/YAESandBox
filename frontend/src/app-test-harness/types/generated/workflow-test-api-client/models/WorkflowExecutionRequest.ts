/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { WorkflowConfig } from './WorkflowConfig';
/**
 * 工作流执行请求的 DTO。
 */
export type WorkflowExecutionRequest = {
    workflowConfig: WorkflowConfig;
    /**
     * 工作流启动所需的触发参数。
     * Key 是参数名，Value 是参数值。
     */
    triggerParams: Record<string, string> | null;
};

