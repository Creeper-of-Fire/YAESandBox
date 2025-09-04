/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateSaveSlotRequest } from '../models/CreateSaveSlotRequest';
import type { SaveSlot } from '../models/SaveSlot';
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class ProjectSaveSlotService {
    /**
     * 获取指定项目的所有存档槽列表。
     * @returns SaveSlot OK
     * @throws ApiError
     */
    public static getApiV1UserDataSaves({
        projectUniqueName,
    }: {
        /**
         * 项目的唯一标识符。
         */
        projectUniqueName: string,
    }): CancelablePromise<Array<SaveSlot>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/user-data/{projectUniqueName}/saves',
            path: {
                'projectUniqueName': projectUniqueName,
            },
            errors: {
                400: `Bad Request`,
            },
        });
    }
    /**
     * 为指定项目创建一个新的存档槽。
     * @returns SaveSlot Created
     * @throws ApiError
     */
    public static postApiV1UserDataSaves({
        projectUniqueName,
        requestBody,
    }: {
        /**
         * 项目的唯一标识符。
         */
        projectUniqueName: string,
        /**
         * 创建请求，包含名称和类型。
         */
        requestBody?: CreateSaveSlotRequest,
    }): CancelablePromise<SaveSlot> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/user-data/{projectUniqueName}/saves',
            path: {
                'projectUniqueName': projectUniqueName,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
            },
        });
    }
    /**
     * 复制一个现有的存档槽，以创建一个内容相同的新槽。
     * @returns SaveSlot Created
     * @throws ApiError
     */
    public static postApiV1UserDataSavesCopy({
        projectUniqueName,
        sourceSlotId,
        requestBody,
    }: {
        /**
         * 项目的唯一标识符。
         */
        projectUniqueName: string,
        /**
         * 要复制的源存档槽的ID。
         */
        sourceSlotId: string,
        /**
         * 描述新存档槽元数据的请求体。
         */
        requestBody?: CreateSaveSlotRequest,
    }): CancelablePromise<SaveSlot> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/api/v1/user-data/{projectUniqueName}/saves/{sourceSlotId}/copy',
            path: {
                'projectUniqueName': projectUniqueName,
                'sourceSlotId': sourceSlotId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * 删除指定项目的一个存档槽。
     * @returns void
     * @throws ApiError
     */
    public static deleteApiV1UserDataSaves({
        projectUniqueName,
        slotId,
    }: {
        /**
         * 项目的唯一标识符。
         */
        projectUniqueName: string,
        /**
         * 要删除的存档槽ID。
         */
        slotId: string,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/user-data/{projectUniqueName}/saves/{slotId}',
            path: {
                'projectUniqueName': projectUniqueName,
                'slotId': slotId,
            },
            errors: {
                404: `未找到指定的存档槽。`,
            },
        });
    }
}
