import type {IScopedStorage} from './IScopedStorage.ts';
import {UserSaveDataService} from "../types/generated/player-save-api-client";

export class ApiScopedStorage implements IScopedStorage
{
    async getItem<T>(token: string, fileName: string): Promise<T | null>
    {
        const jsonString = await UserSaveDataService.getApiV1UserDataUserSaves({
            token: token,
            filename: fileName,
        });
        return JSON.parse(jsonString) as T;
    }

    async setItem<T>(token: string, fileName: string, value: T): Promise<void>
    {
        // 后端 API 需要一个 JSON 字符串
        const requestBody = JSON.stringify(value);
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