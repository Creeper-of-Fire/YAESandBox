/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { RuneValidationResult } from './RuneValidationResult';
import type { ValidationMessage } from './ValidationMessage';
/**
 * 单个步骤的校验结果。
 */
export type StepValidationResult = {
    /**
     * 该步骤内每个符文的校验结果。
     * Key是符文的ConfigId。
     */
    runeResults: Record<string, RuneValidationResult>;
    /**
     * 仅针对步骤本身的校验信息。
     */
    stepMessages: Array<ValidationMessage>;
};

