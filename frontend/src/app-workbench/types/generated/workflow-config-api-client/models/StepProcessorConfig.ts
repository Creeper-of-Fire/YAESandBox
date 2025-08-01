/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractModuleConfig } from './AbstractModuleConfig';
/**
 * 步骤的配置
 */
export type StepProcessorConfig = {
    /**
     * 名字
     */
    name: string;
    /**
     * 是否被启用，默认为True
     */
    enabled: boolean;
    /**
     * 唯一的 ID，在拷贝时也需要更新
     */
    configId: string;
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
    /**
     * 定义了此步骤如何从工作流的全局变量池获取输入，并映射到步骤内部使用的变量名。
     * Key: 步骤内部期望的变量名 (模块消费的名字)
     * Value: 全局变量名 (在工作流中可用的名字)
     */
    inputMappings: Record<string, string>;
};

