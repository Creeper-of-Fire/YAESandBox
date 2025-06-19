/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractModuleConfig } from '../models/AbstractModuleConfig';
import type { AbstractModuleConfigJsonResultDto } from '../models/AbstractModuleConfigJsonResultDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class ModuleConfigService {
    /**
     * 获取所有注册的模块配置类型的表单 Schema 结构 (JSON Schema 格式，包含 UI 指令)。
     * 用于前端动态生成这些类型配置的【新建】或【编辑】表单骨架。
     * @returns any OK
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalModulesAllModuleConfigsSchemas(): CancelablePromise<Record<string, any>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-modules/all-module-configs-schemas',
            errors: {
                500: `Internal Server Error`,
            },
        });
    }
    /**
     * 获取所有全局模块配置的列表。
     * @returns AbstractModuleConfigJsonResultDto 成功获取所有全局模块配置的列表。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalModules(): CancelablePromise<Record<string, AbstractModuleConfigJsonResultDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-modules',
            errors: {
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 获取指定 ID 的全局模块配置。
     * @returns AbstractModuleConfig 成功获取指定的模块配置。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalModules1({
        moduleId,
    }: {
        /**
         * 模块配置的唯一 ID。
         */
        moduleId: string,
    }): CancelablePromise<AbstractModuleConfig> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-modules/{moduleId}',
            path: {
                'moduleId': moduleId,
            },
            errors: {
                404: `未找到指定 ID 的模块配置。`,
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 创建或更新全局模块配置 (Upsert)。
     * 如果指定 ID 的模块配置已存在，则更新它；如果不存在，则创建它。
     * 前端负责生成并提供模块的唯一 ID (GUID)。
     * @returns void
     * @throws ApiError
     */
    public static putApiV1WorkflowsConfigsGlobalModules({
        moduleId,
        requestBody,
    }: {
        /**
         * 要创建或更新的模块配置的唯一 ID。
         */
        moduleId: string,
        /**
         * 模块配置数据。
         */
        requestBody?: AbstractModuleConfig,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/v1/workflows-configs/global-modules/{moduleId}',
            path: {
                'moduleId': moduleId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `请求无效，例如：请求体为空或格式错误。`,
                500: `保存配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 删除指定 ID 的全局模块配置。
     * @returns void
     * @throws ApiError
     */
    public static deleteApiV1WorkflowsConfigsGlobalModules({
        moduleId,
    }: {
        /**
         * 要删除的模块配置的唯一 ID。
         */
        moduleId: string,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/workflows-configs/global-modules/{moduleId}',
            path: {
                'moduleId': moduleId,
            },
            errors: {
                500: `删除配置时发生内部服务器错误。`,
            },
        });
    }
}
