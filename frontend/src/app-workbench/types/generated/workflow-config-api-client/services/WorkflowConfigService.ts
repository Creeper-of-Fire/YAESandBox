/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { WorkflowProcessorConfig } from '../models/WorkflowProcessorConfig.ts';
import type { CancelablePromise } from '../core/CancelablePromise.ts';
import { OpenAPI } from '../core/OpenAPI.ts';
import { request as __request } from '../core/request.ts';
export class WorkflowConfigService {
    /**
     * 获取所有全局工作流配置的列表。
     * @returns WorkflowProcessorConfig 成功获取所有全局工作流配置的列表。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalWorkflows(): CancelablePromise<Array<WorkflowProcessorConfig>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-workflows',
            errors: {
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 获取指定 ID 的全局工作流配置。
     * @returns WorkflowProcessorConfig 成功获取指定的工作流配置。
     * @throws ApiError
     */
    public static getApiV1WorkflowsConfigsGlobalWorkflows1({
        workflowId,
    }: {
        /**
         * 工作流配置的唯一 ID。
         */
        workflowId: string,
    }): CancelablePromise<WorkflowProcessorConfig> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/workflows-configs/global-workflows/{workflowId}',
            path: {
                'workflowId': workflowId,
            },
            errors: {
                404: `未找到指定 ID 的工作流配置。`,
                500: `获取配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 创建或更新全局工作流配置 (Upsert)。
     * @returns void
     * @throws ApiError
     */
    public static putApiV1WorkflowsConfigsGlobalWorkflows({
        workflowId,
        requestBody,
    }: {
        /**
         * 要创建或更新的工作流配置的唯一 ID。
         */
        workflowId: string,
        /**
         * 工作流配置数据。
         */
        requestBody?: WorkflowProcessorConfig,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/v1/workflows-configs/global-workflows/{workflowId}',
            path: {
                'workflowId': workflowId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                500: `保存配置时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 删除指定 ID 的全局工作流配置。
     * @returns void
     * @throws ApiError
     */
    public static deleteApiV1WorkflowsConfigsGlobalWorkflows({
        workflowId,
    }: {
        /**
         * 要删除的工作流配置的唯一 ID。
         */
        workflowId: string,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/workflows-configs/global-workflows/{workflowId}',
            path: {
                'workflowId': workflowId,
            },
            errors: {
                500: `删除配置时发生内部服务器错误。`,
            },
        });
    }
}
