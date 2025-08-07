/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { RuneAnalysisRequest } from '../models/RuneAnalysisRequest';
import type { RuneAnalysisResult } from '../models/RuneAnalysisResult';
import type { TuumAnalysisResult } from '../models/TuumAnalysisResult';
import type { TuumConfig } from '../models/TuumConfig';
import type { WorkflowConfig } from '../models/WorkflowConfig';
import type { WorkflowValidationReport } from '../models/WorkflowValidationReport';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class WorkflowAnalysisService {
    /**
     * 对单个符文配置进行全面的分析和校验。
     * 用于符文编辑器在用户修改配置时，实时获取其数据依赖和校验状态。
     * @returns RuneAnalysisResult OK
     * @throws ApiError
     */
    public static postApiV1WorkflowsConfigsAnalysisAnalyzeRune({
        requestBody,
    }: {
        /**
         * 包含符文配置及其可选上下文的请求体。
         */
        requestBody?: RuneAnalysisRequest,
    }): CancelablePromise<RuneAnalysisResult> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/workflows-configs/analysis/analyze-rune',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * 对单个枢机（Tuum）配置草稿进行高级分析（不包含符文级校验）。
     * 用于枢机编辑器在用户进行连接、映射等操作时，分析枢机的接口、内部变量和数据流。
     * 具体的符文级校验由符文校验端点负责。
     * @returns TuumAnalysisResult OK
     * @throws ApiError
     */
    public static postApiV1WorkflowsConfigsAnalysisAnalyzeTuum({
        requestBody,
    }: {
        /**
         * 枢机配置的完整草稿。
         */
        requestBody?: TuumConfig,
    }): CancelablePromise<TuumAnalysisResult> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/workflows-configs/analysis/analyze-tuum',
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
        requestBody?: WorkflowConfig,
    }): CancelablePromise<WorkflowValidationReport> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/workflows-configs/analysis/validate-workflow',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
