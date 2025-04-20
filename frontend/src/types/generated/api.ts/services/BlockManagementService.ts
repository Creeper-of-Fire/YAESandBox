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
     * @returns void
     * @throws ApiError
     */
    public static deleteApiManageBlocks({
        blockId,
        recursive = false,
        force = false,
    }: {
        blockId: string,
        recursive?: boolean,
        force?: boolean,
    }): CancelablePromise<void> {
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
                400: `Bad Request`,
                404: `Not Found`,
                409: `Conflict`,
                500: `Internal Server Error`,
            },
        });
    }
}
