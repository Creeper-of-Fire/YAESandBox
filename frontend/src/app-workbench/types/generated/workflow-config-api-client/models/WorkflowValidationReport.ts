/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { StepValidationResult } from './StepValidationResult';
/**
 * 整个工作流的校验报告。
 */
export type WorkflowValidationReport = {
    /**
     * 每个步骤的校验结果。
     * Key是步骤的ConfigId。
     */
    stepResults: Record<string, StepValidationResult>;
};

