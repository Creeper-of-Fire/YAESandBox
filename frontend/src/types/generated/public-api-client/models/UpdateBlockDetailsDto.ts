/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 用于通过 PATCH 请求部分更新 Block 的内容和元数据。
 * 任何设置为 null 的属性表示不修改该部分。
 */
export type UpdateBlockDetailsDto = {
    /**
     * (可选) 要设置的新的 Block 内容。
     * 如果为 null，则不修改 BlockContent。
     */
    content?: string | null;
    /**
     * (可选) 要更新或移除的元数据键值对。
     * - Key: 要操作的元数据键。
     * - Value:
     * - 如果为非 null 字符串: 添加或更新该键的值。
     * - 如果为 null: 从元数据中移除该键。
     * 如果整个字典为 null，则不修改 Metadata。
     */
    metadataUpdates?: Record<string, string | null> | null;
};

