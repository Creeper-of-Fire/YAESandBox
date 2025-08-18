/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AbstractRuneConfig } from './AbstractRuneConfig';
import type { TuumInputMapping } from './TuumInputMapping';
import type { TuumOutputMapping } from './TuumOutputMapping';
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
     * 定义内部变量数据来源的列表。这是持久化和与前端交互的主要字段。
     */
    inputMappingsList: Array<TuumInputMapping>;
    /**
     * 定义内部变量如何驱动输出端点的列表。这是持久化和与前端交互的主要字段。
     */
    outputMappingsList: Array<TuumOutputMapping>;
};

