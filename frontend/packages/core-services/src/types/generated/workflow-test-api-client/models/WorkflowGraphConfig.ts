/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { WorkflowConnection } from './WorkflowConnection';
/**
 * 封装了工作流中枢机(Tuum)图的连接配置。
 */
export type WorkflowGraphConfig = {
    /**
     * 控制是否启用基于命名约定的自动连接功能。
     *
     * 当为 true 时，系统会尝试自动连接所有枢机，忽略下面的 Connections 列表。
     * 当为 false 时，系统将严格使用 Connections 列表进行手动连接。
     */
    enableAutoConnect: boolean;
    /**
     * 当 EnableAutoConnect 为 false 时，用于定义工作流中所有枢机之间的显式连接。
     */
    connections?: Array<WorkflowConnection> | null;
};

