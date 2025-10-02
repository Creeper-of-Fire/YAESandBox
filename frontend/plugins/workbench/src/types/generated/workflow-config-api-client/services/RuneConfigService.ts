/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractRuneConfig } from '../models/AbstractRuneConfig';
import type { AbstractRuneConfigStoredConfig } from '../models/AbstractRuneConfigStoredConfig';
import type { AbstractRuneConfigStoredConfigJsonResultDto } from '../models/AbstractRuneConfigStoredConfigJsonResultDto';
import type { RuneSchemasResponse } from '../models/RuneSchemasResponse';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class RuneConfigService {
    /**
     * 获取所有注册的符文配置类型的表单 Schema 结构 (JSON Schema 格式，包含 UI 指令)，并附带它们依赖的动态前端组件资源。
     * 用于前端动态生成这些类型配置的【新建】或【编辑】表单骨架。
     * @returns RuneSchemasResponse OK
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalRunesAllRuneConfigsSchemas(): CancelablePromise<RuneSchemasResponse> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-runes/all-rune-configs-schemas',
            errors: {
                500: `Internal Server Error`,
            },
        });
    }
    /**
     * 获取所有全局符文配置的列表。
     * @returns AbstractRuneConfigStoredConfigJsonResultDto 成功获取所有全局符文配置的列表。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalRunes(): CancelablePromise<Record<string, AbstractRuneConfigStoredConfigJsonResultDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-runes',
            errors: {
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 获取指定 ID 的全局符文配置。
     * @returns AbstractRuneConfigStoredConfig 成功获取指定的符文配置。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalRunes1({
        storeId,
    }: {
        /**
         * 符文配置的唯一 ID。
         */
        storeId: string,
    }): CancelablePromise<AbstractRuneConfigStoredConfig> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-runes/{storeId}',
            path: {
                'storeId': storeId,
            },
            errors: {
                404: `未找到指定 ID 的符文配置。`,
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 创建或更新全局符文配置 (Upsert)。
     * 如果指定 ID 的符文配置已存在，则更新它；如果不存在，则创建它。
     * 前端负责生成并提供符文的唯一 ID (GUID)。
     * @returns void
     * @throws ApiError
     */
    public static putApiV1WorkflowsConfigsGlobalRunes({
        storeId,
        requestBody,
    }: {
        /**
         * 要创建或更新的符文配置的唯一 ID。
         */
        storeId: string,
        /**
         * 符文配置数据。
         */
        requestBody?: AbstractRuneConfigStoredConfig,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/v1/workflows-configs/global-runes/{storeId}',
            path: {
                'storeId': storeId,
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
     * 删除指定 ID 的全局符文配置。
     * @returns void
     * @throws ApiError
     */
    public static deleteApiV1WorkflowsConfigsGlobalRunes({
        storeId,
    }: {
        /**
         * 要删除的符文配置的唯一 ID。
         */
        storeId: string,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/workflows-configs/global-runes/{storeId}',
            path: {
                'storeId': storeId,
            },
            errors: {
                500: `删除配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 根据符文类型名，权威地创建一个新的、包含所有默认值的符文配置实例。
     * 此端点是前端新建任何符文的【唯一】入口，它解决了默认值（包括[DefaultValue]特性）覆盖的核心问题。
     * @returns AbstractRuneConfig 成功返回了默认配置的实例。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalRunesNewRune({
        runeTypeName,
    }: {
        /**
         * 符文的类型名称，例如 "StaticVariableRuneConfig"。
         */
        runeTypeName: string,
    }): CancelablePromise<AbstractRuneConfig> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-runes/new-rune/{runeTypeName}',
            path: {
                'runeTypeName': runeTypeName,
            },
            errors: {
                404: `未找到指定的符文类型。`,
                500: `在实例化或处理默认值时发生内部错误。`,
            },
        });
    }
}
