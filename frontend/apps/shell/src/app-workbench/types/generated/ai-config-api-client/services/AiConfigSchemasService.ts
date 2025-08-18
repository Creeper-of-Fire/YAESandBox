/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AiConfigTypeWithSchemaDto } from '../models/AiConfigTypeWithSchemaDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AiConfigSchemasService {
    /**
     * 获取所有可用的 AI 配置【类型定义及对应的UI Schema】。
     * 此端点一次性返回所有信息，用于前端高效构建配置选择列表和动态表单，取代了之前先获取类型列表再逐个请求 Schema 的流程。
     * @returns AiConfigTypeWithSchemaDto 成功获取所有 AI 配置类型的定义及其 Schema。
     * @throws ApiError
     */
    public static getApiAiConfigurationManagementDefinitions(): CancelablePromise<Array<AiConfigTypeWithSchemaDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-configuration-management/definitions',
            errors: {
                500: `生成 Schema 过程中发生内部服务器错误。`,
            },
        });
    }
}
