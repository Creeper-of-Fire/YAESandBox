/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type {GameStateDto} from '../models/GameStateDto';
import type {UpdateGameStateRequestDto} from '../models/UpdateGameStateRequestDto';
import type {CancelablePromise} from '../core/CancelablePromise';
import {OpenAPI} from '../core/OpenAPI';
import {request as __request} from '../core/request';

export class GameStateService
{
    /**
     * 获取指定 Block 的当前 GameState。
     * @returns GameStateDto 成功返回 GameState。
     * @throws ApiError
     */
    public static getApiBlocksGameState({
                                            blockId,
                                        }: {
        /**
         * 目标 Block 的 ID。
         */
        blockId: string,
    }): CancelablePromise<GameStateDto>
    {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/blocks/{blockId}/GameState',
            path: {
                'blockId': blockId,
            },
            errors: {
                404: `未找到具有指定 ID 的 Block。`,
            },
        });
    }

    /**
     * 修改指定 Block 的 GameState。使用 PATCH 方法进行部分更新。
     * @returns void
     * @throws ApiError
     */
    public static patchApiBlocksGameState({
                                              blockId,
                                              requestBody,
                                          }: {
        /**
         * 目标 Block 的 ID。
         */
        blockId: string,
        /**
         * 包含要更新的 GameState 键值对的请求体。
         */
        requestBody?: UpdateGameStateRequestDto,
    }): CancelablePromise<void>
    {
        return __request(OpenAPI, {
            method: 'PATCH',
            url: '/api/blocks/{blockId}/GameState',
            path: {
                'blockId': blockId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `请求体无效。`,
                404: `未找到具有指定 ID 的 Block。`,
                500: `更新时发生内部服务器错误。`,
            },
        });
    }
}
