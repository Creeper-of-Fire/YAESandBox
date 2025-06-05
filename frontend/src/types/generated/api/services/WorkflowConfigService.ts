/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type {WorkflowProcessorConfig} from '../models/WorkflowProcessorConfig';
import type {CancelablePromise} from '../core/CancelablePromise';
import {OpenAPI} from '../core/OpenAPI';
import {request as __request} from '../core/request';

export class WorkflowConfigService
{
    /**
     * @returns WorkflowProcessorConfig OK
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalWorkflows(): CancelablePromise<Array<WorkflowProcessorConfig>>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-workflows',
            errors: {
                500: `Internal Server Error`,
            },
        });
    }

    /**
     * @returns WorkflowProcessorConfig OK
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalWorkflows1({
                                                               workflowId,
                                                           }: {
        workflowId: string,
    }): CancelablePromise<WorkflowProcessorConfig>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-workflows/{workflowId}',
            path: {
                'workflowId': workflowId,
            },
            errors: {
                404: `Not Found`,
                500: `Internal Server Error`,
            },
        });
    }

    /**
     * @returns void
     * @throws ApiError
     */
    public static putApiV1WorkflowsConfigsGlobalWorkflows({
                                                              workflowId,
                                                              requestBody,
                                                          }: {
        workflowId: string,
        requestBody?: WorkflowProcessorConfig,
    }): CancelablePromise<void>
    {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/v1/workflows-configs/global-workflows/{workflowId}',
            path: {
                'workflowId': workflowId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                500: `Internal Server Error`,
            },
        });
    }

    /**
     * @returns void
     * @throws ApiError
     */
    public static deleteApiV1WorkflowsConfigsGlobalWorkflows({
                                                                 workflowId,
                                                             }: {
        workflowId: string,
    }): CancelablePromise<void>
    {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/workflows-configs/global-workflows/{workflowId}',
            path: {
                'workflowId': workflowId,
            },
            errors: {
                500: `Internal Server Error`,
            },
        });
    }
}
