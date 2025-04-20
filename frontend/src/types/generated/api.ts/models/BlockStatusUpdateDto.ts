/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { BlockStatusCode } from './BlockStatusCode';
/**
 * (服务器 -> 客户端)
 * 通知客户端某个 Block 的状态码发生了变化。
 */
export type BlockStatusUpdateDto = {
    /**
     * 状态发生变化的 Block 的 ID。
     */
    blockId: string;
    statusCode: BlockStatusCode;
};

