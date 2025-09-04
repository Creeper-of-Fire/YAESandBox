/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';
export class UserSaveDataService {
    /**
     * 在Token指定的位置创建或更新一个JSON资源。
     * @returns void
     * @throws ApiError
     */
    public static putApiV1UserDataUserSaves({
        filename,
        token,
        requestBody,
    }: {
        /**
         * 要操作的文件名
         */
        filename: string,
        /**
         * 访问令牌，用于唯一地定位一个资源容器的位置。
         */
        token: string,
        /**
         * 要存储的 JSON 字符串。
         */
        requestBody: string,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'PUT',
            url: '/api/v1/user-data/user-saves/{filename}',
            path: {
                'filename': filename,
            },
            query: {
                'token': token,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `请求无效，例如：Token无效或 JSON 格式错误。`,
            },
        });
    }
    /**
     * 读取Token指定位置的JSON资源。
     * @returns string 成功返回资源内容。
     * @throws ApiError
     */
    public static getApiV1UserDataUserSaves({
        filename,
        token,
    }: {
        /**
         * 要操作的文件名
         */
        filename: string,
        /**
         * 访问令牌，用于唯一地定位一个资源容器的位置。
         */
        token: string,
    }): CancelablePromise<string> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/user-data/user-saves/{filename}',
            path: {
                'filename': filename,
            },
            query: {
                'token': token,
            },
            errors: {
                404: `未找到指定的资源。`,
            },
        });
    }
    /**
     * 删除Token指定位置的JSON资源。
     * @returns void
     * @throws ApiError
     */
    public static deleteApiV1UserDataUserSaves({
        filename,
        token,
    }: {
        /**
         * 要操作的文件名
         */
        filename: string,
        /**
         * 访问令牌，用于唯一地定位一个资源容器的位置。
         */
        token: string,
    }): CancelablePromise<void> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/api/v1/user-data/user-saves/{filename}',
            path: {
                'filename': filename,
            },
            query: {
                'token': token,
            },
            errors: {
                404: `未找到要删除的资源。`,
            },
        });
    }
    /**
     * 列出在Token指定位置下的所有资源名称。
     * @returns string 成功返回资源名称列表（可能为空）。
     * @throws ApiError
     */
    public static getApiV1UserDataUserSavesList({
        token,
    }: {
        /**
         * 访问令牌，代表要列出内容的容器位置。
         */
        token: string,
    }): CancelablePromise<Array<string>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/api/v1/user-data/user-saves/list',
            query: {
                'token': token,
            },
            errors: {
                400: `Token包含无效字符。`,
            },
        });
    }
}
