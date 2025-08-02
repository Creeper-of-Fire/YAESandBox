/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { TuumValidationResult } from './TuumValidationResult';
/**
 * 整个工作流的校验报告。
 */
export type WorkflowValidationReport = {
    /**
     * 每个祝祷的校验结果。
     * Key是祝祷的ConfigId。
     */
    tuumResults: Record<string, TuumValidationResult>;
};

