/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractRuneConfig } from './AbstractRuneConfig';
import type { TuumConfig } from './TuumConfig';
/**
 * 为 analyze-rune 端点设计的请求体。
 */
export type RuneAnalysisRequest = {
    runeToAnalyze: AbstractRuneConfig;
    tuumContext?: TuumConfig;
};

