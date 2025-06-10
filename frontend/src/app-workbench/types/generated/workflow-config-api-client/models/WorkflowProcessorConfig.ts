/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { StepProcessorConfig } from './StepProcessorConfig';
/**
 * 工作流的配置
 */
export type WorkflowProcessorConfig = {
    /**
     * 名字
     */
    name: string;
    /**
     * 是否被启用，默认为True
     */
    enabled: boolean;
    /**
     * 声明此工作流启动时需要提供的触发参数列表。
     * 用于校验和前端提示。
     */
    triggerParams: Array<string>;
    /**
     * 一个工作流含有的步骤（有序）
     */
    steps: Array<StepProcessorConfig>;
};

