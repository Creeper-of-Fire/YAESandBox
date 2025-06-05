/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type {StepProcessorConfig} from '../models/StepProcessorConfig';
import type {CancelablePromise} from '../core/CancelablePromise';
import {OpenAPI} from '../core/OpenAPI';
import {request as __request} from '../core/request';

export class StepConfigService
{
    /**
     * @returns StepProcessorConfig OK
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalSteps(): CancelablePromise<Array<StepProcessorConfig>>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-steps',
            errors: {
                500: `Internal Server Error`,
            },
        });
    }

    /**
     * @returns StepProcessorConfig OK
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalSteps1({
                                                           stepId,
                                                       }: {
        stepId: string,
    }): CancelablePromise<StepProcessorConfig>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-steps/{stepId}',
            path: {
                'stepId': stepId,
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
    public static putApiV1WorkflowsConfigsGlobalSteps({
                                                          stepId,
                                                          requestBody,
                                                      }: {
        stepId: string,
        requestBody?: StepProcessorConfig,
    }): CancelablePromise<void>
    {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/v1/workflows-configs/global-steps/{stepId}',
            path: {
                'stepId': stepId,
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
    public static deleteApiV1WorkflowsConfigsGlobalSteps({
                                                             stepId,
                                                         }: {
        stepId: string,
    }): CancelablePromise<void>
    {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/workflows-configs/global-steps/{stepId}',
            path: {
                'stepId': stepId,
            },
            errors: {
                500: `Internal Server Error`,
            },
        });
    }
}
