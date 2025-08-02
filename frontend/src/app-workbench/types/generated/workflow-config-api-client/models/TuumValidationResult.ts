/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { RuneValidationResult } from './RuneValidationResult';
import type { ValidationMessage } from './ValidationMessage';
/**
 * 单个祝祷的校验结果。
 */
export type TuumValidationResult = {
    /**
     * 该祝祷内每个符文的校验结果。
     * Key是符文的ConfigId。
     */
    runeResults: Record<string, RuneValidationResult>;
    /**
     * 仅针对祝祷本身的校验信息。
     */
    tuumMessages: Array<ValidationMessage>;
};

