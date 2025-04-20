/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * (客户端 -> 服务器)
 * 用于通过 SignalR 请求重新生成现有 Block 的内容和状态。
 * 只有主工作流对此有用。
 */
export type RegenerateBlockRequestDto = {
    /**
     * 唯一的请求 ID，用于追踪。
     */
    requestId: string;
    /**
     * 要重新生成的 Block 的 ID。
     */
    blockId: string;
    /**
     * 用于重新生成的工作流名称。
     */
    workflowName: string;
    /**
     * 传递给重新生成工作流的参数。
     */
    params?: Record<string, any> | null;
};

