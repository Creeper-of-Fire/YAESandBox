/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { BlockStatusCode } from './BlockStatusCode';
/**
 * 用于 API 响应，表示单个 Block 的详细信息（不包含 WorldState）。
 */
export type BlockDetailDto = {
    /**
     * Block 的唯一标识符。
     */
    blockId: string;
    statusCode?: BlockStatusCode;
    /**
     * Block 的主要文本内容 (例如 AI 生成的文本、配置等)。
     */
    blockContent?: string | null;
    /**
     * 与 Block 相关的元数据字典 (键值对均为字符串)。
     */
    metadata?: Record<string, string> | null;
};

