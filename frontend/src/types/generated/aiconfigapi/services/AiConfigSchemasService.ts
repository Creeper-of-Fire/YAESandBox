/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type {SelectOptionDto} from '../models/SelectOptionDto';
import type {CancelablePromise} from '../core/CancelablePromise';
import {OpenAPI} from '../core/OpenAPI';
import {request as __request} from '../core/request';

export class AiConfigSchemasService
{
    /**
     * 获取指定 AI 配置类型的表单 Schema 结构 (JSON Schema 格式，包含 ui: 指令)。
     * 用于前端动态生成该类型配置的【新建】或【编辑】表单骨架。
     * @returns any 成功获取指定配置类型的 JSON Schema。
     * @throws ApiError
     */
    public static getApiAiConfigurationManagementSchemas({
                                                             configTypeName,
                                                         }: {
        /**
         * AI 配置的类型名称 (例如 "DoubaoAiProcessorConfig")。
         */
        configTypeName: string,
    }): CancelablePromise<any>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-configuration-management/schemas/{configTypeName}',
            path: {
                'configTypeName': configTypeName,
            },
            errors: {
                400: `请求的配置类型名称无效。`,
                404: `未找到指定名称的配置类型。`,
                500: `生成 Schema 时发生内部服务器错误。`,
            },
        });
    }

    /**
     * 获取所有可用的 AI 配置【类型定义】列表。
     * 用于前端展示可以【新建】哪些类型的 AI 配置。
     * @returns SelectOptionDto 成功获取所有可用的 AI 配置类型列表。
     * @throws ApiError
     */
    public static getApiAiConfigurationManagementAvailableConfigTypes(): CancelablePromise<Array<SelectOptionDto>>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/ai-configuration-management/available-config-types',
        });
    }
}
