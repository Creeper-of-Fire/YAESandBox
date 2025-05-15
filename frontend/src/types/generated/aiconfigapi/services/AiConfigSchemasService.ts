/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AiConfigSchemasService {
    /**
     * @deprecated
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
}
