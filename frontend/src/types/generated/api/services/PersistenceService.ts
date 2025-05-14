/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type {CancelablePromise} from '../core/CancelablePromise';
import {OpenAPI} from '../core/OpenAPI';
import {request as __request} from '../core/request';

export class PersistenceService {
    /**
     * 保存当前 YAESandBox 的完整状态（包括所有 Block、WorldState、GameState）到一个 JSON 文件。
     * 客户端可以（可选地）在请求体中提供需要一同保存的“盲存”数据。
     * @returns binary 成功生成并返回存档文件。
     * @throws ApiError
     */
    public static postApiPersistenceSave({
                                             requestBody,
                                         }: {
        /**
         * （可选）客户端提供的任意 JSON 格式的盲存数据，将原样保存在存档文件中。
         */
        requestBody?: any,
    }): CancelablePromise<Blob> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/Persistence/save',
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                500: `保存状态时发生内部服务器错误。`,
            },
        });
    }

    /**
     * 从上传的 JSON 存档文件加载 YAESandBox 的状态。
     * 这将完全替换当前内存中的所有 Block、WorldState 和 GameState。
     * 成功加载后，将返回存档文件中包含的“盲存”数据（如果存在）。
     * @returns any 成功加载状态，并返回盲存数据。
     * @throws ApiError
     */
    public static postApiPersistenceLoad({
                                             formData,
                                         }: {
        formData?: {
            /**
             * 包含 YAESandBox 存档的 JSON 文件。
             */
            archiveFile?: Blob;
        },
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/Persistence/load',
            formData: formData,
            mediaType: 'multipart/form-data',
            errors: {
                400: `没有上传文件，或者上传的文件格式无效 (非 JSON 或内容损坏)。`,
                500: `加载状态时发生内部服务器错误。`,
            },
        });
    }
}
