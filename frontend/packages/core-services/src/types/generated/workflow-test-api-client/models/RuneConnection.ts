/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { RuneConnectionEndpoint } from './RuneConnectionEndpoint';
/**
 * 定义了Tuum内部两个Rune端口之间的一条有向连接。
 */
export type RuneConnection = {
    source: RuneConnectionEndpoint;
    target: RuneConnectionEndpoint;
};

