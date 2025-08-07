/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { RuneValidationResult } from './RuneValidationResult';
import type { ValidationMessage } from './ValidationMessage';
/**
 * 单个枢机的校验结果。
 */
export type TuumValidationResult = {
    /**
     * 该枢机内每个符文的校验结果。
     * Key是符文的ConfigId。
     */
    runeResults: Record<string, RuneValidationResult>;
    /**
     * 仅针对枢机本身的校验信息。
     */
    tuumMessages: Array<ValidationMessage>;
};

