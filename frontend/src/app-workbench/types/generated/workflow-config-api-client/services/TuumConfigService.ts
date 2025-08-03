/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { TuumConfig } from '../models/TuumConfig';
import type { TuumConfigJsonResultDto } from '../models/TuumConfigJsonResultDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class TuumConfigService {
    /**
     * 获取所有全局祝祷配置的列表。
     * @returns TuumConfigJsonResultDto 成功获取所有全局祝祷配置的列表。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalTuums(): CancelablePromise<Record<string, TuumConfigJsonResultDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-tuums',
            errors: {
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 获取指定 ID 的全局祝祷配置。
     * @returns TuumConfig 成功获取指定的祝祷配置。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalTuums1({
        tuumId,
    }: {
        /**
         * 祝祷配置的唯一 ID。
         */
        tuumId: string,
    }): CancelablePromise<TuumConfig> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-tuums/{tuumId}',
            path: {
                'tuumId': tuumId,
            },
            errors: {
                404: `未找到指定 ID 的祝祷配置。`,
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 创建或更新全局祝祷配置 (Upsert)。
     * @returns void
     * @throws ApiError
     */
    public static putApiV1WorkflowsConfigsGlobalTuums({
        tuumId,
        requestBody,
    }: {
        /**
         * 要创建或更新的祝祷配置的唯一 ID。
         */
        tuumId: string,
        /**
         * 祝祷配置数据。
         */
        requestBody?: TuumConfig,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/v1/workflows-configs/global-tuums/{tuumId}',
            path: {
                'tuumId': tuumId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                500: `保存配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 删除指定 ID 的全局祝祷配置。
     * @returns void
     * @throws ApiError
     */
    public static deleteApiV1WorkflowsConfigsGlobalTuums({
        tuumId,
    }: {
        /**
         * 要删除的祝祷配置的唯一 ID。
         */
        tuumId: string,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/workflows-configs/global-tuums/{tuumId}',
            path: {
                'tuumId': tuumId,
            },
            errors: {
                500: `删除配置时发生内部服务器错误。`,
            },
        });
    }
}
