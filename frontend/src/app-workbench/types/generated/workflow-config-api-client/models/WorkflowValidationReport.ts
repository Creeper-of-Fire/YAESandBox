/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { TuumAnalysisResult } from './TuumAnalysisResult';
import type { ValidationMessage } from './ValidationMessage';
/**
 * 整个工作流的校验报告。
 */
export type WorkflowValidationReport = {
    /**
     * 每个枢机的校验结果。
     * Key是枢机的ConfigId。
     */
    tuumResults: Record<string, TuumAnalysisResult>;
    /**
     * Key: Connection的唯一标识符
     */
    connectionMessages: Record<string, Array<ValidationMessage>>;
    /**
     * 用于存放循环依赖等无法归属到任何单一实体的错误
     */
    globalMessages: Array<ValidationMessage>;
};

