/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type {IModuleConfig} from './IModuleConfig';
import type {StepAiConfig} from './StepAiConfig';

export type StepProcessorConfig = {
    instanceId: string;
    stepAiConfig?: StepAiConfig;
    modules?: Array<IModuleConfig> | null;
};

