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
