/**
 * 负责在特定作用域（由 token 标识）内读写数据。
 * 这是与后端 UserSaveDataService API 交互的低层端口。
 * 它取代了旧的、基于路径的 StorageAdapter。
 */
export interface IScopedStorage {
    /**
     * 读取指定作用域下文件的内容。
     * @param token - 标识存档槽作用域的访问令牌。
     * @param fileName - 文件名 (例如 'characters.json')。
     * @returns 解析后的文件内容，或在文件不存在时返回 null。
     */
    getItem<T>(token: string, fileName: string): Promise<T | null>;

    /**
     * 在指定作用域下创建或覆盖一个文件。
     * @param token - 标识存档槽作用域的访问令牌。
     * @param fileName - 文件名。
     * @param value - 要存储的数据，将被序列化为 JSON。
     */
    setItem<T>(token: string, fileName: string, value: T): Promise<void>;

    /**
     * 删除指定作用域下的一个文件。
     * @param token - 标识存档槽作用域的访问令牌。
     * @param fileName - 文件名。
     */
    removeItem(token: string, fileName: string): Promise<void>;

    /**
     * 列出指定作用域下的所有文件名。
     * @param token - 标识存档槽作用域的访问令牌。
     * @returns 包含所有文件名的字符串数组。
     */
    list(token: string): Promise<string[]>;
}