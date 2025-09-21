/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { RuneConnection } from './RuneConnection';
/**
 * 封装了Tuum内部Rune图的连接配置。
 */
export type TuumGraphConfig = {
    /**
     * 控制是否启用基于命名约定的自动连接功能。
     *
     * 当为 true 时，系统会尝试自动连接所有Rune，忽略下面的 Connections 列表。
     * 当为 false 时，系统将严格使用 Connections 列表进行手动连接。
     */
    enableAutoConnect: boolean;
    /**
     * 当 EnableAutoConnect 为 false 时，用于定义枢机内部所有Rune之间的显式连接。
     */
    connections?: Array<RuneConnection> | null;
};

