/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
/**
 * 用于序列化为 JSON 的内部节点表示。
 */
export type JsonBlockNode = {
    /**
     * 节点 ID。
     */
    id: string;
    /**
     * 子节点列表。
     */
    readonly children: Array<JsonBlockNode>;
};

