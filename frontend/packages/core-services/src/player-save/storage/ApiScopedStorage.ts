import type {IScopedStorage} from './IScopedStorage.ts';
import {UserSaveDataService} from "../../types/generated/player-save-api-client";

export class ApiScopedStorage implements IScopedStorage
{
    async getItem<T>(token: string, fileName: string): Promise<T | null>
    {
        // try {
        const jsonString = await UserSaveDataService.getApiV1UserDataUserSaves({
            token: token,
            filename: fileName,
        });
        if (jsonString === "")
            return null
        return jsonString as T;
        // } catch (error: any) {
        //     // API 客户端在 404 时会抛出 ApiError
        //     if (error.name === 'ApiError' && error.status === 404) {
        //         return null;
        //     }
        //     // 其他错误则重新抛出
        //     throw error;
        // }
    }

    async setItem<T>(token: string, fileName: string, value: T): Promise<void>
    {
        const requestBody = value;

        await UserSaveDataService.putApiV1UserDataUserSaves({
            token: token,
            filename: fileName,
            requestBody: requestBody,
        });
    }

    async removeItem(token: string, fileName: string): Promise<void>
    {
        await UserSaveDataService.deleteApiV1UserDataUserSaves({
            token: token,
            filename: fileName,
        });
    }

    async list(token: string): Promise<string[]>
    {
        return UserSaveDataService.getApiV1UserDataUserSavesList({
            token: token,
        });
    }
}