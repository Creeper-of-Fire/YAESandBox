import type {IProjectMetaStorage} from './IProjectMetaStorage';
import type {IScopedStorage} from './IScopedStorage';
import {ProjectSaveSlotService} from "../types/generated/player-save-api-client";

export class ApiProjectMetaStorage implements IProjectMetaStorage
{
    private readonly projectUniqueName: string;
    private readonly scopedStorage: IScopedStorage;
    private metaToken: string | null = null; // 用于缓存 meta token

    constructor(projectUniqueName: string, scopedStorage: IScopedStorage)
    {
        this.projectUniqueName = projectUniqueName;
        this.scopedStorage = scopedStorage;
    }

    async getItem<T>(key: string): Promise<T | null>
    {
        const token = await this._getMetaToken();
        return this.scopedStorage.getItem<T>(token, key);
    }

    async setItem<T>(key: string, value: T): Promise<void>
    {
        const token = await this._getMetaToken();
        await this.scopedStorage.setItem(token, key, value);
    }

    /**
     * 内部方法，用于获取并缓存项目元数据的访问 token。
     */
    private async _getMetaToken(): Promise<string>
    {
        if (this.metaToken)
        {
            return this.metaToken;
        }

        const token = await ProjectSaveSlotService.getApiV1UserDataSavesMeta({
            projectUniqueName: this.projectUniqueName,
        });

        if (!token)
        {
            throw new Error(`在获得项目 "${this.projectUniqueName}" 的meta存储token时失败。`);
        }

        this.metaToken = token;
        return token;
    }
}