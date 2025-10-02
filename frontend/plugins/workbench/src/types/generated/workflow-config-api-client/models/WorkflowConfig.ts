/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { TuumConfig } from './TuumConfig';
import type { WorkflowGraphConfig } from './WorkflowGraphConfig';
/**
 * 工作流的配置
 */
export type WorkflowConfig = {
    name: string;
    /**
     * 声明此工作流启动时需要提供的入口参数列表。
     * 这些输入可以作为连接的源头。
     */
    workflowInputs: Array<string>;
    /**
     * 一个工作流含有的枢机（有序）
     */
    tuums: Array<TuumConfig>;
    graph?: WorkflowGraphConfig;
    /**
     * 工作流的标签，属于元数据，用于筛选等。
     */
    tags?: Array<string> | null;
};

