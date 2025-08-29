import localforage from 'localforage';

/**
 * 定义了一个标准化的存储适配器接口。
 * 任何持久化方案（localforage, localStorage, API）都应该实现这个接口。
 */
export interface StorageAdapter {
    getItem<T>(key: string): Promise<T | null>;
    setItem<T>(key: string, value: T): Promise<void>;
    removeItem(key: string): Promise<void>;
}

/**
 * 一个基于 localforage 的存储适配器实现。
 */
export const localforageAdapter: StorageAdapter = {
    async getItem<T>(key:string): Promise<T | null> {
        return await localforage.getItem<T>(key);
    },
    async setItem<T>(key: string, value: T): Promise<void> {
        await localforage.setItem(key, value);
    },
    async removeItem(key: string): Promise<void> {
        await localforage.removeItem(key);
    },
};

// 未来可以轻松添加更多适配器，例如：
/*
export const localStorageAdapter: StorageAdapter = {
  async getItem<T>(key: string): Promise<T | null> {
    const data = localStorage.getItem(key);
    return data ? JSON.parse(data) : null;
  },
  async setItem<T>(key: string, value: T): Promise<void> {
    localStorage.setItem(key, JSON.stringify(value));
  },
  async removeItem(key: string): Promise<void> {
    localStorage.removeItem(key);
  }
};
*/