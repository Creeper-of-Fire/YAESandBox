/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { TuumConfigStoredConfig } from '../models/TuumConfigStoredConfig';
import type { TuumConfigStoredConfigJsonResultDto } from '../models/TuumConfigStoredConfigJsonResultDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class TuumConfigService {
    /**
     * 获取所有全局枢机配置的列表。
     * @returns TuumConfigStoredConfigJsonResultDto 成功获取所有全局枢机配置的列表。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalTuums(): CancelablePromise<Record<string, TuumConfigStoredConfigJsonResultDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-tuums',
            errors: {
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 获取指定 ID 的全局枢机配置。
     * @returns TuumConfigStoredConfig 成功获取指定的枢机配置。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalTuums1({
        storeId,
    }: {
        /**
         * 枢机配置的唯一 ID。
         */
        storeId: string,
    }): CancelablePromise<TuumConfigStoredConfig> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-tuums/{storeId}',
            path: {
                'storeId': storeId,
            },
            errors: {
                404: `未找到指定 ID 的枢机配置。`,
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 创建或更新全局枢机配置 (Upsert)。
     * @returns void
     * @throws ApiError
     */
    public static putApiV1WorkflowsConfigsGlobalTuums({
        storeId,
        requestBody,
    }: {
        /**
         * 要创建或更新的枢机配置的唯一 ID。
         */
        storeId: string,
        /**
         * 枢机配置数据。
         */
        requestBody?: TuumConfigStoredConfig,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/v1/workflows-configs/global-tuums/{storeId}',
            path: {
                'storeId': storeId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                500: `保存配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 删除指定 ID 的全局枢机配置。
     * @returns void
     * @throws ApiError
     */
    public static deleteApiV1WorkflowsConfigsGlobalTuums({
        storeId,
    }: {
        /**
         * 要删除的枢机配置的唯一 ID。
         */
        storeId: string,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/workflows-configs/global-tuums/{storeId}',
            path: {
                'storeId': storeId,
            },
            errors: {
                500: `删除配置时发生内部服务器错误。`,
            },
        });
    }
}
