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
    configId: string;
    stepAiConfig?: StepAiConfig;
    /**
     * 按顺序执行的模块列表。
     * StepProcessor 在执行时会严格按照此列表的顺序执行模块。
     */
    modules: Array<AbstractModuleConfig>;
    /**
     * 定义了此步骤如何将其内部变量暴露到工作流的全局变量池。
     * Key: 全局变量名 (在工作流中使用的名字)
     * Value: 步骤内部的变量名 (由模块产生的名字)
     */
    outputMappings: Record<string, string>;
};

