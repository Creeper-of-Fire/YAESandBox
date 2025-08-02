/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractAiProcessorConfig } from '../models/AbstractAiProcessorConfig';
import type { AiConfigurationSet } from '../models/AiConfigurationSet';
import type { TestAiDto } from '../models/TestAiDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AiConfigurationsService {
    /**
     * 获取所有已保存的 AI 配置集的完整列表。
     * @returns AiConfigurationSet 成功获取所有 AI 配置集的列表。
     * @throws ApiError
     */
    public static getApiAiConfigurations(): CancelablePromise<Record<string, AiConfigurationSet>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-configurations',
            errors: {
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 根据 UUID 获取一个特定的 AI 配置集。
     * @returns AiConfigurationSet 成功获取指定的 AI 配置集。
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
                400: `请求的 UUID 无效（例如，为空）。`,
                404: `未找到指定 UUID 的配置集。`,
            },
        });
    }
    /**
     * 创建或更新一个 AI 配置集。此操作是幂等的。
     * @returns any 配置集已成功创建。
     * @throws ApiError
     */
    public static putApiAiConfigurations({
        uuid,
        requestBody,
    }: {
        /**
         * 要创建或更新的配置集的唯一标识符（由客户端提供）。
         */
        uuid: string,
        /**
         * 包含完整信息的 AI 配置集对象。
         */
        requestBody?: AiConfigurationSet,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/ai-configurations/{uuid}',
            path: {
                'uuid': uuid,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `请求无效，例如 UUID 为空或请求体无效。`,
                500: `保存配置时发生内部服务器错误。`,
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
                400: `请求的 UUID 无效。`,
                500: `删除配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 测试Ai配置
     * @returns string AI 配置测试成功，返回 AI 生成的完整文本。
     * @throws ApiError
     */
    public static postApiAiConfigurationsAiConfigTest({
        aiModelType,
        requestBody,
    }: {
        aiModelType: string,
        /**
         * 配置和测试文本。
         */
        requestBody?: TestAiDto,
    }): CancelablePromise<string> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/ai-configurations/ai-config-test/{aiModelType}',
            path: {
                'aiModelType': aiModelType,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                500: `测试期间发生错误，例如 AI 服务调用失败。`,
            },
        });
    }
    /**
     * @deprecated
     * 获取指定 AI 模型类型的初始默认数据。
     * 用于前端为新配置项生成表单。
     * @returns AbstractAiProcessorConfig 成功获取指定 AI 模型类型的默认数据。
     * @throws ApiError
     */
    public static getApiAiConfigurationsDefaultData({
        aiModelType,
    }: {
        /**
         * AI 模型的类型名称 (例如 "DoubaoAiProcessorConfig")。
         */
        aiModelType: string,
    }): CancelablePromise<AbstractAiProcessorConfig> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-configurations/default-data/{aiModelType}',
            path: {
                'aiModelType': aiModelType,
            },
            errors: {
                400: `请求的模型类型名称无效。`,
                404: `未找到指定名称的 AI 模型类型。`,
                500: `获取默认数据时发生内部服务器错误。`,
            },
        });
    }
}
