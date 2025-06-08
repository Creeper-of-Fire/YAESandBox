/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { BatchAtomicRequestDto } from '../models/BatchAtomicRequestDto';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class AtomicService {
    /**
     * 对指定的 Block 执行一批原子化操作。
     * 根据 Block 的当前状态，操作可能被立即执行或暂存。
     * @returns any 操作已成功执行，若为Loading状态则还额外暂存了一份。
     * @throws ApiError
     */
    public static postApiAtomic({
        blockId,
        requestBody,
    }: {
        /**
         * 要执行操作的目标 Block 的 ID。
         */
        blockId: string,
        /**
         * 包含原子操作列表的请求体。
         */
        requestBody?: BatchAtomicRequestDto,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/atomic/{blockId}',
            path: {
                'blockId': blockId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `请求中包含无效的原子操作定义。`,
                404: `未找到具有指定 ID 的 Block。`,
                409: `Block 当前处于冲突状态 (ResolvingConflict)，需要先解决冲突。`,
                500: `执行操作时发生内部服务器错误。`,
            },
        });
    }
}
