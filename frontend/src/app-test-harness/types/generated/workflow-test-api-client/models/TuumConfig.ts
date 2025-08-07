/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractRuneConfig } from './AbstractRuneConfig';
/**
 * 枢机的配置
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
     * 定义了此枢机可被连接的【输入端点】及其驱动的内部变量。
     * Key: 外部输入端点的名称 (供外部连接使用)。
     * Value: 由该输入端点提供数据的内部变量名列表 (供符文消费)。
     */
    inputMappings: Record<string, Array<string>>;
    /**
     * 定义了此枢机的【内部变量】如何驱动【输出端点】。
     * Key: 提供数据的枢机内部变量名 (由符文产生)。
     * Value: 由该内部变量驱动的外部输出端点名称列表。
     */
    outputMappings: Record<string, Array<string>>;
};

