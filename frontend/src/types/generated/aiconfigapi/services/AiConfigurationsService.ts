/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractAiProcessorConfig } from '../models/AbstractAiProcessorConfig';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AiConfigurationsService {
    /**
     * 获取所有已保存的 AI 配置的完整列表。
     * @returns AbstractAiProcessorConfig OK
     * @throws ApiError
     */
    public static getApiAiConfigurations(): CancelablePromise<Record<string, AbstractAiProcessorConfig>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-configurations',
            errors: {
                500: `Internal Server Error`,
            },
        });
    }
    /**
     * 添加一个新的 AI 配置。
     * @returns string Created
     * @throws ApiError
     */
    public static postApiAiConfigurations({
        requestBody,
    }: {
        /**
         * 要添加的 AI 配置对象。请求体中需要包含 'ModuleType' 辨别器属性。
         */
        requestBody?: AbstractAiProcessorConfig,
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
     * 根据 UUID 获取一个特定的 AI 配置。
     * @returns AbstractAiProcessorConfig OK
     * @throws ApiError
     */
    public static getApiAiConfigurations1({
        uuid,
    }: {
        /**
         * 配置的唯一标识符。
         */
        uuid: string,
    }): CancelablePromise<AbstractAiProcessorConfig> {
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
     * 更新一个已存在的 AI 配置。
     * @returns void
     * @throws ApiError
     */
    public static putApiAiConfigurations({
        uuid,
        requestBody,
    }: {
        /**
         * 要更新的配置的唯一标识符。
         */
        uuid: string,
        /**
         * 包含更新信息的 AI 配置对象。ModuleType 应与现有配置匹配。
         */
        requestBody?: AbstractAiProcessorConfig,
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
     * 根据 UUID 删除一个 AI 配置。
     * @returns void
     * @throws ApiError
     */
    public static deleteApiAiConfigurations({
        uuid,
    }: {
        /**
         * 要删除的配置的唯一标识符。
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
