/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractRuneConfig } from './AbstractRuneConfig';
/**
 * 祝祷的配置
 */
export type TuumConfig = {
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
     * 按顺序执行的符文列表。
     * TuumProcessor 在执行时会严格按照此列表的顺序执行符文。
     */
    runes: Array<AbstractRuneConfig>;
    /**
     * 定义了此祝祷如何将其内部变量暴露到工作流的全局变量池。
     * Key: 全局变量名 (在工作流中使用的名字)
     * Value: 祝祷内部的变量名 (由符文产生的名字)
     */
    outputMappings: Record<string, string>;
    /**
     * 定义了此祝祷如何从工作流的全局变量池获取输入，并映射到祝祷内部使用的变量名。
     * Key: 祝祷内部期望的变量名 (符文消费的名字)
     * Value: 全局变量名 (在工作流中可用的名字)
     */
    inputMappings: Record<string, string>;
};

