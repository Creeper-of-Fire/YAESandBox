/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractModuleConfig } from '../models/AbstractModuleConfig';
import type { ModuleAnalysisResult } from '../models/ModuleAnalysisResult';
import type { WorkflowProcessorConfig } from '../models/WorkflowProcessorConfig';
import type { WorkflowValidationReport } from '../models/WorkflowValidationReport';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class WorkflowAnalysisService {
    /**
     * 分析单个模块配置，动态计算其输入和输出变量。
     * 用于编辑器在用户修改模块配置时，实时获取其数据依赖，以增强智能提示和即时反馈。
     * @returns ModuleAnalysisResult OK
     * @throws ApiError
     */
    public static postApiV1WorkflowsConfigsAnalysisAnalyzeModule({
        requestBody,
    }: {
        /**
         * 包含模块配置草稿的请求体。
         */
        requestBody?: AbstractModuleConfig,
    }): CancelablePromise<ModuleAnalysisResult> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/workflows-configs/analysis/analyze-module',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * 对整个工作流配置草稿进行全面的静态校验。
     * 用于编辑器在用户编辑时，通过防抖调用此API，获取一份完整的“健康报告”，并在UI上高亮所有潜在的逻辑错误和警告。
     * @returns WorkflowValidationReport OK
     * @throws ApiError
     */
    public static postApiV1WorkflowsConfigsAnalysisValidateWorkflow({
        requestBody,
    }: {
        /**
         * 工作流配置的完整草稿。
         */
        requestBody?: WorkflowProcessorConfig,
    }): CancelablePromise<WorkflowValidationReport> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/workflows-configs/analysis/validate-workflow',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
