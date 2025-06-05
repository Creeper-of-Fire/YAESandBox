/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type {EntityDetailDto} from '../models/EntityDetailDto';
import type {EntitySummaryDto} from '../models/EntitySummaryDto';
import type {EntityType} from '../models/EntityType';
import type {CancelablePromise} from '../core/CancelablePromise';
import {OpenAPI} from '../core/OpenAPI';
import {request as __request} from '../core/request';

export class EntitiesService
{
    /**
     * 获取指定 Block 当前可交互 WorldState 中的所有非销毁实体摘要信息。
     * @returns EntitySummaryDto 成功返回实体摘要列表。
     * @throws ApiError
     */
    public static getApiEntities({
                                     blockId,
                                 }: {
        /**
         * 要查询的目标 Block 的 ID。
         */
        blockId: string,
    }): CancelablePromise<Array<EntitySummaryDto>>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/entities',
            query: {
                'blockId': blockId,
            },
            errors: {
                400: `缺少必需的 'blockId' 查询参数。`,
                404: `未找到具有指定 ID 的 Block 或 Block 无法访问。`,
            },
        });
    }

    /**
     * 获取指定 Block 当前可交互 WorldState 中的单个非销毁实体的详细信息。
     * @returns EntityDetailDto 成功返回实体详细信息。
     * @throws ApiError
     */
    public static getApiEntities1({
                                      entityType,
                                      entityId,
                                      blockId,
                                  }: {
        /**
         * 要查询的实体的类型 (Item, Character, Place)。
         */
        entityType: EntityType,
        /**
         * 要查询的实体的 ID。
         */
        entityId: string,
        /**
         * 目标 Block 的 ID。
         */
        blockId: string,
    }): CancelablePromise<EntityDetailDto>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/entities/{entityType}/{entityId}',
            path: {
                'entityType': entityType,
                'entityId': entityId,
            },
            query: {
                'blockId': blockId,
            },
            errors: {
                400: `缺少必需的 'blockId' 查询参数。`,
                404: `未在指定 Block 中找到实体，或 Block 未找到。`,
            },
        });
    }
}
