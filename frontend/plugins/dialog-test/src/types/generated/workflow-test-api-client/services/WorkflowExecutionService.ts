/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { WorkflowExecutionSignalRRequest } from '../models/WorkflowExecutionSignalRRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class WorkflowExecutionService {
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
