/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type {IModuleConfig} from '../models/IModuleConfig';
import type {JsonNode} from '../models/JsonNode';
import type {CancelablePromise} from '../core/CancelablePromise';
import {OpenAPI} from '../core/OpenAPI';
import {request as __request} from '../core/request';

export class ModuleConfigService
{
    /**
     * @returns JsonNode OK
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalModulesAllModuleConfigsSchemas(): CancelablePromise<Record<string, JsonNode>>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-modules/all-module-configs-schemas',
            errors: {
                500: `Internal Server Error`,
            },
        });
    }

    /**
     * @returns IModuleConfig OK
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalModules(): CancelablePromise<Array<IModuleConfig>>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-modules',
            errors: {
                500: `Internal Server Error`,
            },
        });
    }

    /**
     * @returns IModuleConfig OK
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalModules1({
                                                             moduleId,
                                                         }: {
        moduleId: string,
    }): CancelablePromise<IModuleConfig>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-modules/{moduleId}',
            path: {
                'moduleId': moduleId,
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
    public static putApiV1WorkflowsConfigsGlobalModules({
                                                            moduleId,
                                                            requestBody,
                                                        }: {
        moduleId: string,
        requestBody?: IModuleConfig,
    }): CancelablePromise<void>
    {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/v1/workflows-configs/global-modules/{moduleId}',
            path: {
                'moduleId': moduleId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
                500: `Internal Server Error`,
            },
        });
    }

    /**
     * @returns void
     * @throws ApiError
     */
    public static deleteApiV1WorkflowsConfigsGlobalModules({
                                                               moduleId,
                                                           }: {
        moduleId: string,
    }): CancelablePromise<void>
    {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/workflows-configs/global-modules/{moduleId}',
            path: {
                'moduleId': moduleId,
            },
            errors: {
                500: `Internal Server Error`,
            },
        });
    }
}
