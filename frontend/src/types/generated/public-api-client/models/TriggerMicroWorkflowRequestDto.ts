/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * (客户端 -> 服务器)
 * 用于通过 SignalR 触发 **微工作流** 的请求。
 * 微工作流通常用于生成辅助信息、建议或执行不直接改变核心叙事状态（即不创建新 Block）的操作。
 * 其结果通常用于更新 UI 的特定部分。
 */
export type TriggerMicroWorkflowRequestDto = {
    /**
     * 客户端生成的唯一请求 ID，用于追踪此工作流调用的整个生命周期。
     */
    requestId: string;
    /**
     * 触发此微工作流时，用户界面所在的上下文 Block 的 ID。
     * 工作流逻辑可能会使用此 Block 的状态作为输入。
     */
    contextBlockId: string;
    /**
     * (关键) 目标 UI 元素或逻辑区域的标识符。
     * 后端会将此工作流产生的 YAESandBox.Core.DTOs.WebSocket.DisplayUpdateDto 消息的 YAESandBox.Core.DTOs.WebSocket.DisplayUpdateDto.TargetElementId 设置为此值，
     * 以便前端知道更新哪个 UI 组件。该 ID 由前端定义和解释。
     */
    targetElementId: string;
    /**
     * 要调用的微工作流的名称或标识符。
     */
    workflowName: string;
    /**
     * 传递给微工作流的参数字典。
     */
    params: Record<string, any>;
};

