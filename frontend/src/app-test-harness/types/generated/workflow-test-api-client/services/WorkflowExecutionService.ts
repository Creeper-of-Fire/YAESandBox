/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { WorkflowExecutionRequest } from '../models/WorkflowExecutionRequest';
import type { WorkflowExecutionResult } from '../models/WorkflowExecutionResult';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class WorkflowExecutionService {
    /**
     * 执行一个工作流并返回最终结果。
     * 这是一个简化的执行端点，用于快速测试。
     * 它接收一个完整的工作流配置和触发参数，然后同步执行整个流程。
     * 在实际应用中，可能会使用 SignalR 进行流式返回。
     * @returns WorkflowExecutionResult OK
     * @throws ApiError
     */
    public static postApiV1WorkflowExecutionExecute({
        requestBody,
    }: {
        /**
         * 包含工作流配置和触发参数的请求体。
         */
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
     * 以流式方式执行工作流，并通过 Server-Sent Events 返回结果。
     * 此端点用于对话式场景，实时返回AI生成的文本。
     * 它会持续推送更新后的完整文本。
     * @returns any OK
     * @throws ApiError
     */
    public static postApiV1WorkflowExecutionExecuteStream({
        requestBody,
    }: {
        /**
         * 包含工作流配置和触发参数的请求体。
         */
        requestBody?: WorkflowExecutionRequest,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/workflow-execution/execute-stream',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
