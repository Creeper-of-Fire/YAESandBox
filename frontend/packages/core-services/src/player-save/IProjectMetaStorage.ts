/**
 * 负责读写项目级别的元数据。
 * 例如：最后激活的存档 ID、全局用户偏好设置等。
 */
export interface IProjectMetaStorage {
    /**
     * 获取一个项目级的元数据项。
     * @param key - 元数据的键 (会被用作文件名)。
     * @returns 返回元数据的值，如果不存在则返回 null。
     */
    getItem<T>(key: string): Promise<T | null>;

    /**
     * 设置一个项目级的元数据项。
     * @param key - 元数据的键 (会被用作文件名)。
     * @param value - 要存储的元数据值。
     */
    setItem<T>(key: string, value: T): Promise<void>;
}