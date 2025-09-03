import axios from "axios";
import localforage from 'localforage';

/**
 * 定义了一个标准化的、基于文件和路径的存储适配器接口。
 * 任何持久化方案都必须实现这个接口。
 */
export interface StorageAdapter
{
    /**
     * 读取指定路径下文件的内容。
     * @param path 文件的父目录路径数组，例如 ['saves', 'slot-1']。
     * @param fileName 文件名，例如 'characters.json'。
     * @returns 解析后的文件内容，如果文件不存在则返回 null。
     */
    getItem<T>(path:readonly string[], fileName: string): Promise<T | null>;

    /**
     * 在指定路径下创建或覆盖一个文件。
     * @param path 文件的父目录路径数组。
     * @param fileName 文件名。
     * @param value 要存储的数据，将被序列化。
     */
    setItem<T>(path: readonly string[], fileName: string, value: T): Promise<void>;

    /**
     * 删除指定路径下的一个文件。
     * @param path 文件的父目录路径数组。
     * @param fileName 文件名。
     */
    removeItem(path:readonly string[], fileName: string): Promise<void>;

    /**
     * 列出指定路径目录下的所有条目（文件或子目录）。
     * @param path 要列出内容的目录路径。
     * @returns 一个包含文件名或目录名的字符串数组。
     */
    list(path:readonly string[]): Promise<string[]>;
}


const isSuccess = (status: number): boolean =>
{
    return status >= 200 && status < 300;
};

/**
 * 一个基于后端 API 的存储适配器实现。
 * 它将前端的 "path" 和 "fileName" 概念转换为后端的 URL。
 */
class ApiStorageAdapter implements StorageAdapter
{
    async getItem<T>(path: string[], fileName: string): Promise<T | null>
    {
        const fullPath = this._joinPath(path, fileName);

        const response = await axios.get(`/api/v1/test/user-saves/${fullPath}`);
        if (response.status === 404)
        {
            return null;
        }
        if (!isSuccess(response.status))
        {
            throw new Error(`Failed to get item at ${fullPath}: ${response.statusText}`);
        }
        // 后端直接返回 JSON 字符串，所以我们需要解析它
        const rawJsonString = await response.data;
        return JSON.parse(rawJsonString) as T;
    }

    async setItem<T>(path: string[], fileName: string, value: T): Promise<void>
    {
        const fullPath = this._joinPath(path, fileName);
        const jsonData = JSON.stringify(value);
        const response = await axios.put(`/api/v1/test/user-saves/${fullPath}`, {
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify(jsonData) // API 需要一个包含 JSON 字符串的 JSON 字符串体
        });
        if (!isSuccess(response.status))
        {
            throw new Error(`Failed to set item at ${fullPath}: ${response.statusText}`);
        }
    }

    async removeItem(path: string[], fileName: string): Promise<void>
    {
        const fullPath = this._joinPath(path, fileName);
        const response = await axios.delete(`/api/v1/test/user-saves/${fullPath}`);
        if (!isSuccess(response.status) && response.status !== 404)
        {
            throw new Error(`Failed to remove item at ${fullPath}: ${response.statusText}`);
        }
    }

    async list(path: string[]): Promise<string[]>
    {
        const fullPath = this._joinPath(path);
        const response = await axios.get(`/api/v1/test/user-saves/list/${fullPath}`);
        if (!isSuccess(response.status))
        {
            throw new Error(`Failed to list items at ${fullPath}: ${response.statusText}`);
        }
        return await response.data;
    }

    // 辅助函数，将路径数组和文件名合并为 API 需要的 URL 片段
    private _joinPath(path: string[], fileName?: string): string
    {
        return [...path, ...(fileName ? [fileName] : [])].join('/');
    }
}

/**
 * 一个基于 localforage 的存储适配器实现。
 * 它在 localforage 的键值存储之上模拟了一个目录结构。
 *
 * 内部存储策略:
 * - 文件: key 为 'file::{path}/{fileName}'，value 为文件内容。
 * - 目录列表: key 为 'dir::{path}'，value 为一个 Set<string>，包含文件名和子目录名。
 */
class LocalforageAdapter implements StorageAdapter
{
    async getItem<T>(path: string[], fileName: string): Promise<T | null>
    {
        const fileKey = this._getFileKey(path, fileName);
        return await localforage.getItem<T>(fileKey);
    }

    async setItem<T>(path: string[], fileName: string, value: T): Promise<void>
    {
        const fileKey = this._getFileKey(path, fileName);
        const dirKey = this._getDirKey(path);

        // 1. 保存文件内容
        await localforage.setItem(fileKey, value);

        // 2. 更新当前目录的列表
        const dirSet = await localforage.getItem<Set<string>>(dirKey) ?? new Set();
        if (!dirSet.has(fileName))
        {
            dirSet.add(fileName);
            await localforage.setItem(dirKey, dirSet);
        }

        // 3. 确保父目录也知道这个子目录的存在
        if (path.length > 0)
        {
            await this._updateParentDir(path, path[path.length - 1], 'add');
        }
    }

    async removeItem(path: string[], fileName: string): Promise<void>
    {
        const fileKey = this._getFileKey(path, fileName);
        const dirKey = this._getDirKey(path);

        // 1. 删除文件
        await localforage.removeItem(fileKey);

        // 2. 从目录列表中移除文件条目
        const dirSet = await localforage.getItem<Set<string>>(dirKey);
        if (dirSet && dirSet.has(fileName))
        {
            dirSet.delete(fileName);
            if (dirSet.size === 0)
            {
                // 如果目录变空，则删除目录列表本身
                await localforage.removeItem(dirKey);
                // 并从其父目录中移除该空目录的条目
                if (path.length > 0)
                {
                    await this._updateParentDir(path, path[path.length - 1], 'remove');
                }
            }
            else
            {
                await localforage.setItem(dirKey, dirSet);
            }
        }
    }

    async list(path: string[]): Promise<string[]>
    {
        const dirKey = this._getDirKey(path);
        const dirSet = await localforage.getItem<Set<string>>(dirKey);
        // 将 Set 转换为数组返回
        return dirSet ? Array.from(dirSet) : [];
    }

    private _getDirKey(path: string[]): string
    {
        return `dir::${path.join('/')}`;
    }

    private _getFileKey(path: string[], fileName: string): string
    {
        return `file::${[...path, fileName].join('/')}`;
    }

    // 递归更新父目录的条目列表
    private async _updateParentDir(path: string[], entryName: string, operation: 'add' | 'remove'): Promise<void>
    {
        if (path.length === 0) return; // 根目录没有父目录

        const parentPath = path.slice(0, -1);
        const dirName = path[path.length - 1];
        const parentDirKey = this._getDirKey(parentPath);

        const parentDirSet = await localforage.getItem<Set<string>>(parentDirKey) ?? new Set();

        if (operation === 'add')
        {
            if (parentDirSet.has(dirName)) return; // 已经存在，无需操作
            parentDirSet.add(dirName);
        }
        else
        {
            if (!parentDirSet.has(dirName)) return; // 不存在，无需操作
            parentDirSet.delete(dirName);
        }

        await localforage.setItem(parentDirKey, parentDirSet);
        // 递归向上更新，确保整个路径都被正确索引
        await this._updateParentDir(parentPath, dirName, operation);
    }
}

// 导出适配器的单例实例
export const apiStorageAdapter: StorageAdapter = new ApiStorageAdapter();
export const localforageAdapter: StorageAdapter = new LocalforageAdapter();