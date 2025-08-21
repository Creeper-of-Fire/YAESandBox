/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { WorkflowExecutionRequest } from '../models/WorkflowExecutionRequest';
import type { WorkflowExecutionResult } from '../models/WorkflowExecutionResult';
import type { WorkflowExecutionSignalRRequest } from '../models/WorkflowExecutionSignalRRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class WorkflowExecutionService {
    /**
     * 执行一个工作流并返回最终结果（以结构化文本形式）。
     * 这是一个简化的同步执行端点。它会捕获所有发射的事件，
     * 在内存中构建一个结构化的XML响应，并最终返回完整的XML字符串。
     * @returns WorkflowExecutionResult OK
     * @throws ApiError
     */
    public static postApiV1WorkflowExecutionExecute({
        requestBody,
    }: {
        requestBody?: WorkflowExecutionRequest,
    }): CancelablePromise<WorkflowExecutionResult> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/workflow-execution/execute',
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
            },
        });
    }
    /**
     * 通过 SignalR 异步触发一个工作流执行，并流式推送结果。
     * 此端点会立即返回202 Accepted状态，表示任务已接受。
     * 实际的工作流在后台执行，并通过与请求中 `ConnectionId` 关联的 SignalR 连接推送事件。
     * 客户端需要先建立SignalR连接，获取`ConnectionId`，然后调用此API。
     * @returns any Accepted
     * @throws ApiError
     */
    public static postApiV1WorkflowExecutionExecuteSignalr({
        requestBody,
    }: {
        requestBody?: WorkflowExecutionSignalRRequest,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/workflow-execution/execute-signalr',
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
            },
        });
    }
}
