/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 表示扁平化拓扑结构中的单个节点信息。
 */
export type BlockTopologyNodeDto = {
    /**
     * Block 的唯一标识符。
     */
    blockId: string;
    /**
     * 父 Block 的 ID。如果为根节点，则为 null。
     */
    parentBlockId?: string | null;
};

