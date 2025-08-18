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
     * 以流式方式执行工作流，并通过 Server-Sent Events 返回结构化结果。
     * 此端点使用我们新的事件系统。工作流中的 "EmitEventRune" 会触发事件，
     * 此处会将这些事件实时地构建成一个XML结构，并将每次更新后的完整XML通过SSE推送给前端。
     * @returns any OK
     * @throws ApiError
     */
    public static postApiV1WorkflowExecutionExecuteStream({
        requestBody,
    }: {
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
