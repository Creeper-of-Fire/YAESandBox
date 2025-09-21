/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ConsumedSpec } from './ConsumedSpec';
import type { ProducedSpec } from './ProducedSpec';
import type { ValidationMessage } from './ValidationMessage';
/**
 * 单个符文的校验结果。
 */
export type RuneAnalysisResult = {
    /**
     * 符文消费的输入参数
     */
    consumedVariables: Array<ConsumedSpec>;
    /**
     * 符文生产的输出参数
     */
    producedVariables: Array<ProducedSpec>;
    /**
     * 针对该符文的校验信息列表。
     */
    runeMessages: Array<ValidationMessage>;
};

