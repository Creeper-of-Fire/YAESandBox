/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * (客户端 -> 服务器)
 * 用于通过 SignalR 触发 **主工作流** 的请求。
 * 主工作流通常会导致创建一个新的子 Block 来表示新的叙事状态。
 */
export type TriggerMainWorkflowRequestDto = {
    /**
     * 客户端生成的唯一请求 ID，用于追踪此工作流调用的整个生命周期，
     * 包括可能的流式更新和最终结果。
     */
    requestId: string;
    /**
     * 要在其下创建新子 Block 的父 Block 的 ID。
     */
    parentBlockId: string;
    /**
     * 要调用的工作流的名称或标识符。
     */
    workflowName: string;
    /**
     * 传递给工作流的参数字典。键值对的具体内容取决于所调用的工作流。
     */
    params?: Record<string, any> | null;
};

