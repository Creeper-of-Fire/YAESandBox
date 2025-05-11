/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { SelectOption } from '../models/SelectOption';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AiConfigSchemasService {
    /**
     * 获取指定 AI 配置类型的表单 Schema 结构 (JSON Schema 格式，包含 ui: 指令)。
     * 用于前端动态生成该类型配置的【新建】或【编辑】表单骨架。
     * @returns any OK
     * @throws ApiError
     */
    public static getApiAiConfigurationManagementSchemas({
        configTypeName,
    }: {
        /**
         * AI 配置的类型名称 (例如 "DoubaoAiProcessorConfig")。
         */
        configTypeName: string,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-configuration-management/schemas/{configTypeName}',
            path: {
                'configTypeName': configTypeName,
            },
            errors: {
                400: `Bad Request`,
                404: `Not Found`,
                500: `Internal Server Error`,
            },
        });
    }
    /**
     * 获取所有可用的 AI 配置【类型定义】列表。
     * 用于前端展示可以【新建】哪些类型的 AI 配置。
     * @returns SelectOption OK
     * @throws ApiError
     */
    public static getApiAiConfigurationManagementAvailableConfigTypes(): CancelablePromise<Array<SelectOption>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-configuration-management/available-config-types',
        });
    }
}
