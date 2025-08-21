/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { WorkflowConfig } from './WorkflowConfig';
/**
 * 用于 SignalR 触发的工作流执行请求的 DTO。
 */
export type WorkflowExecutionSignalRRequest = {
    workflowConfig: WorkflowConfig;
    /**
     * 工作流启动所需的触发参数。
     * Key 是参数名，Value 是参数值。
     */
    workflowInputs: Record<string, string>;
    /**
     * 客户端的 SignalR 连接 ID。
     * 服务器将通过此 ID 将流式结果推送给正确的客户端。
     */
    connectionId: string;
};

