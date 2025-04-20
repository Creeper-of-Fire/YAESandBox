/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { BlockDetailDto } from '../models/BlockDetailDto';
import type { JsonBlockNode } from '../models/JsonBlockNode';
import type { UpdateBlockDetailsDto } from '../models/UpdateBlockDetailsDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class BlocksService {
    /**
     * 获取所有 Block 的摘要信息字典。
     * 返回一个以 Block ID 为键，Block 详细信息 DTO 为值的只读字典。
     * @returns BlockDetailDto 成功返回 Block 字典。
     * @throws ApiError
     */
    public static getApiBlocks(): CancelablePromise<Record<string, BlockDetailDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/Blocks',
        });
    }
    /**
     * 获取指定 ID 的单个 Block 的详细信息（不包含 WorldState）。
     * @returns BlockDetailDto 成功返回 Block 详细信息。
     * @throws ApiError
     */
    public static getApiBlocks1({
        blockId,
    }: {
        /**
         * 要查询的 Block 的唯一 ID。
         */
        blockId: string,
    }): CancelablePromise<BlockDetailDto> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/Blocks/{blockId}',
            path: {
                'blockId': blockId,
            },
            errors: {
                404: `未找到具有指定 ID 的 Block。`,
            },
        });
    }
    /**
     * 部分更新指定 Block 的内容和/或元数据。
     * 此操作仅在 Block 处于 Idle 状态时被允许。
     * @returns void
     * @throws ApiError
     */
    public static patchApiBlocks({
        blockId,
        requestBody,
    }: {
        /**
         * 要更新的 Block 的 ID。
         */
        blockId: string,
        /**
         * 包含要更新的字段（Content, MetadataUpdates）的请求体。
         * 省略的字段或值为 null 的字段将不会被修改（MetadataUpdates 中值为 null 表示移除该键）。
         */
        requestBody?: UpdateBlockDetailsDto,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'PATCH',
            url: '/api/Blocks/{blockId}',
            path: {
                'blockId': blockId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `请求体无效或未提供任何更新。`,
                404: `未找到具有指定 ID 的 Block。`,
                409: `Block 不处于 Idle 状态，无法修改。`,
                500: `更新时发生内部服务器错误。`,
            },
        });
    }
    /**
     * 获取整个 Block 树的拓扑结构 (基于 ID 的嵌套关系)。
     * 返回一个表示 Block 树层级结构的 JSON 对象。
     * @returns JsonBlockNode 成功返回 JSON 格式的拓扑结构。
     * 形如：{ "id": "__WORLD__", "children": [{ "id": "child1", "children": [] },{ "id": "child2", "children": [] }] }
     * @throws ApiError
     */
    public static getApiBlocksTopology({
        blockId,
    }: {
        /**
         * 目标根节点的ID，如果为空则返回整个父节点的ID
         */
        blockId?: string,
    }): CancelablePromise<JsonBlockNode> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/Blocks/topology',
            query: {
                'blockId': blockId,
            },
            errors: {
                500: `生成拓扑结构时发生内部服务器错误。`,
            },
        });
    }
}
