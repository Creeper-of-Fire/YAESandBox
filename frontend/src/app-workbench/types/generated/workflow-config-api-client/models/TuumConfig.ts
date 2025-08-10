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
     * 定义了内部变量所需数据的来源，即从哪个外部输入端点获取。
     * 此结构以“内部需求”为起点，描述了“我这个变量，需要的数据从哪里来”。
     * 这种设计更符合用户配置时的直观逻辑。
     */
    inputMappings: Record<string, string>;
    /**
     * 定义了此枢机的【内部变量】如何驱动【输出端点】。
     * Key: 提供数据的枢机内部变量名 (由符文产生)。
     * Value: 由该内部变量驱动的外部输出端点名称列表。
     */
    outputMappings: Record<string, Array<string>>;
};

