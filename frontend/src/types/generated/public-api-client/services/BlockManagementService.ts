/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class BlockManagementService {
    /**
     * 手动删除一个指定的 Block。
     * @returns any 操作已成功执行。
     * @throws ApiError
     */
    public static deleteApiManageBlocks({
        blockId,
        recursive = true,
        force = false,
    }: {
        /**
         * 要删除的 Block ID。
         */
        blockId: string,
        /**
         * 是否递归删除子 Block。默认递归删除，非递归可能导致奇奇怪怪的问题？
         */
        recursive?: boolean,
        /**
         * 是否强制删除，无视状态。
         */
        force?: boolean,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/manage/blocks/{blockId}',
            path: {
                'blockId': blockId,
            },
            query: {
                'recursive': recursive,
                'force': force,
            },
            errors: {
                400: `不允许删除根节点或请求无效（例如 Block 有子节点但未指定 recursive=true）。`,
                404: `未找到具有指定 ID 的 Block。`,
                409: `Block的当前状态不允许删除。现在只允许删除Idle或Error，说实在的有点意义不明，也许以后设计成什么都可以删除。`,
                500: `执行操作时发生内部服务器错误。`,
            },
        });
    }
}
