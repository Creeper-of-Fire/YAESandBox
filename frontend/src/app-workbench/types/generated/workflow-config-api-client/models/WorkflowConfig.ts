/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { TuumConfig } from './TuumConfig';
import type { WorkflowConnection } from './WorkflowConnection';
/**
 * 工作流的配置
 */
export type WorkflowConfig = {
    /**
     * 名字
     */
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
    /**
     * 定义了工作流中所有枢机之间的显式连接。
     * 这是工作流数据流向的唯一依据。
     */
    connections: Array<WorkflowConnection>;
};

