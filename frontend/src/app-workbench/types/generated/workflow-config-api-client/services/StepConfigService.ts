/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { StepProcessorConfig } from '../models/StepProcessorConfig';
import type { StepProcessorConfigSingleItemResultDto } from '../models/StepProcessorConfigSingleItemResultDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class StepConfigService {
    /**
     * 获取所有全局步骤配置的列表。
     * @returns StepProcessorConfigSingleItemResultDto 成功获取所有全局步骤配置的列表。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalSteps(): CancelablePromise<Record<string, StepProcessorConfigSingleItemResultDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-steps',
            errors: {
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 获取指定 ID 的全局步骤配置。
     * @returns StepProcessorConfig 成功获取指定的步骤配置。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalSteps1({
        stepId,
    }: {
        /**
         * 步骤配置的唯一 ID。
         */
        stepId: string,
    }): CancelablePromise<StepProcessorConfig> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-steps/{stepId}',
            path: {
                'stepId': stepId,
            },
            errors: {
                404: `未找到指定 ID 的步骤配置。`,
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 创建或更新全局步骤配置 (Upsert)。
     * @returns void
     * @throws ApiError
     */
    public static putApiV1WorkflowsConfigsGlobalSteps({
        stepId,
        requestBody,
    }: {
        /**
         * 要创建或更新的步骤配置的唯一 ID。
         */
        stepId: string,
        /**
         * 步骤配置数据。
         */
        requestBody?: StepProcessorConfig,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/v1/workflows-configs/global-steps/{stepId}',
            path: {
                'stepId': stepId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                500: `保存配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 删除指定 ID 的全局步骤配置。
     * @returns void
     * @throws ApiError
     */
    public static deleteApiV1WorkflowsConfigsGlobalSteps({
        stepId,
    }: {
        /**
         * 要删除的步骤配置的唯一 ID。
         */
        stepId: string,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/workflows-configs/global-steps/{stepId}',
            path: {
                'stepId': stepId,
            },
            errors: {
                500: `删除配置时发生内部服务器错误。`,
            },
        });
    }
}
