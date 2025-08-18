/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { WorkflowConfig } from './WorkflowConfig';
export type WorkflowConfigJsonResultDto = {
    isSuccess: boolean;
    data: WorkflowConfig;
    errorMessage: string | null;
    /**
     * 失败时，有可能返回序列化错误时的原始文本
     */
    originJsonString: string | null;
};

