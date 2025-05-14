/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type {BlockDetailDto} from '../models/BlockDetailDto';
import type {BlockTopologyNodeDto} from '../models/BlockTopologyNodeDto';
import type {UpdateBlockDetailsDto} from '../models/UpdateBlockDetailsDto';
import type {CancelablePromise} from '../core/CancelablePromise';
import {OpenAPI} from '../core/OpenAPI';
import {request as __request} from '../core/request';

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
     * 获取扁平化的 Block 拓扑结构信息。
     * 返回一个包含所有 Block (或指定子树下所有 Block) 的拓扑信息的列表，
     * 每个对象包含其 ID 和父节点 ID，用于在客户端重建层级关系。
     * @returns BlockTopologyNodeDto 成功返回扁平化的拓扑节点列表。
     * 列表中的每个对象形如：{ "blockId": "some-id", "parentBlockId": "parent-id" } 或 { "blockId": "__WORLD__", "parentBlockId": null }。
     * 例如：[ { "blockId": "__WORLD__", "parentBlockId": null }, { "blockId": "child1", "parentBlockId": "__WORLD__" }, ... ]
     * @throws ApiError
     */
    public static getApiBlocksTopology({
                                           blockId,
                                       }: {
        /**
         * （可选）目标根节点的 ID。
         * 如果提供，则返回以此节点为根的子树（包含自身）的扁平拓扑信息。
         * 如果为 null 或空，则返回从最高根节点 (__WORLD__) 开始的整个应用的完整扁平拓扑结构。
         */
        blockId?: string,
    }): CancelablePromise<Array<BlockTopologyNodeDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/Blocks/topology',
            query: {
                'blockId': blockId,
            },
            errors: {
                404: `如果指定了 blockId，但未找到具有该 ID 的 Block。`,
                500: `获取拓扑结构时发生内部服务器错误。`,
            },
        });
    }
}
