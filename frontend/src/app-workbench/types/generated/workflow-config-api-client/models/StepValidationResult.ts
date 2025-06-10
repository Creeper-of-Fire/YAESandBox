/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ModuleValidationResult } from './ModuleValidationResult';
import type { ValidationMessage } from './ValidationMessage';
/**
 * 单个步骤的校验结果。
 */
export type StepValidationResult = {
    /**
     * 该步骤内每个模块的校验结果。
     * Key是模块的ConfigId。
     */
    moduleResults: Record<string, ModuleValidationResult>;
    /**
     * 仅针对步骤本身的校验信息。
     */
    stepMessages: Array<ValidationMessage>;
};

