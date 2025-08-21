// models.ts
// 使用 interface 定义我们的核心数据结构

/**
 * 角色定义
 */
export interface Character {
    id: string;
    name: string;
    description: string;
    avatar: string; // 可以是 emoji 或图片 URL
}

/**
 * 场景定义
 */
export interface Scene {
    id: string;
    name: string;
    description: string;
}

/**
 * 道具定义
 */
export interface Item {
    id: string;
    name: string;
    description: string;
    price: number;
}