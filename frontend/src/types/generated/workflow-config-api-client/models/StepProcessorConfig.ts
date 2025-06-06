/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractModuleConfig } from './AbstractModuleConfig';
import type { StepAiConfig } from './StepAiConfig';
export type StepProcessorConfig = {
    /**
     * 唯一的 ID，在拷贝时也需要更新
     */
    instanceId: string;
    stepAiConfig?: StepAiConfig;
    /**
     * 按顺序执行的模块列表。
     * StepProcessor 在执行时会严格按照此列表的顺序执行模块。
     */
    modules?: Array<AbstractModuleConfig> | null;
};

