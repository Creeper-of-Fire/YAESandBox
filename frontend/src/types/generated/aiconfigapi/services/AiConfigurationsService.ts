/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AiConfigurationSet } from '../models/AiConfigurationSet';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AiConfigurationsService {
    /**
     * 获取所有已保存的 AI 配置集的完整列表。
     * @returns AiConfigurationSet OK
     * @throws ApiError
     */
    public static getApiAiConfigurations(): CancelablePromise<Record<string, AiConfigurationSet>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-configurations',
            errors: {
                500: `Internal Server Error`,
            },
        });
    }
    /**
     * 添加一个新的 AI 配置集。
     * @returns string Created
     * @throws ApiError
     */
    public static postApiAiConfigurations({
        requestBody,
    }: {
        /**
         * 要添加的 AI 配置集对象。
         */
        requestBody?: AiConfigurationSet,
    }): CancelablePromise<string> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/ai-configurations',
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
                500: `Internal Server Error`,
            },
        });
    }
    /**
     * 根据 UUID 获取一个特定的 AI 配置集。
     * @returns AiConfigurationSet OK
     * @throws ApiError
     */
    public static getApiAiConfigurations1({
        uuid,
    }: {
        /**
         * 配置集的唯一标识符。
         */
        uuid: string,
    }): CancelablePromise<AiConfigurationSet> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-configurations/{uuid}',
            path: {
                'uuid': uuid,
            },
            errors: {
                400: `Bad Request`,
                404: `Not Found`,
            },
        });
    }
    /**
     * 更新一个已存在的 AI 配置集。
     * @returns void
     * @throws ApiError
     */
    public static putApiAiConfigurations({
        uuid,
        requestBody,
    }: {
        /**
         * 要更新的配置集的唯一标识符。
         */
        uuid: string,
        /**
         * 包含更新信息的 AI 配置集对象。
         */
        requestBody?: AiConfigurationSet,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/ai-configurations/{uuid}',
            path: {
                'uuid': uuid,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
                404: `Not Found`,
                500: `Internal Server Error`,
            },
        });
    }
    /**
     * 根据 UUID 删除一个 AI 配置集。
     * @returns void
     * @throws ApiError
     */
    public static deleteApiAiConfigurations({
        uuid,
    }: {
        /**
         * 要删除的配置集的唯一标识符。
         */
        uuid: string,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/ai-configurations/{uuid}',
            path: {
                'uuid': uuid,
            },
            errors: {
                400: `Bad Request`,
                500: `Internal Server Error`,
            },
        });
    }
}
