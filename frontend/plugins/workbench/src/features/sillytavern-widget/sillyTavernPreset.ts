// file: sillyTavernPreset.ts

/**
 * 对应 C# 的 PromptRoleType 枚举
 */
export enum PromptRole {
    System = 'System',
    User = 'User',
    Assistant = 'Assistant',
}

/**
 * 基础的提示词项，包含所有可能的字段
 */
interface PromptItemBase {
    identifier: string;
    name: string;
    system_prompt: boolean;
    marker: boolean;
    enabled?: boolean; // 这个字段在 'prompts' 列表中有时会出现
    [key: string]: unknown;
}

/**
 * 标记类型的提示词项 (例如 chatHistory, scenario)
 */
export interface MarkerPromptItem extends PromptItemBase {
    marker: true;
    content?: null;
    role?: null;
    injection_position?: null;
    injection_depth?: null;
    injection_order?: null;
}

/**
 * 带有实际内容的提示词项
 */
export interface ContentPromptItem extends PromptItemBase {
    marker: false;
    content: string;
    role: 'system' | 'user' | 'assistant'; // 限制为有效值
    injection_position?: 0 | 1;
    injection_depth?: number;
    injection_order?: number;
}

/**
 * 使用联合类型，让 TypeScript 能够根据 'marker' 属性推断类型
 * 这在 v-if 判断中非常有用
 */
export type PromptItem = MarkerPromptItem | ContentPromptItem;


/**
 * 对应 C# 的 OrderItem，定义了在顺序列表中的一个引用
 */
export interface OrderItem {
    identifier: string;
    enabled: boolean;
    [key: string]: unknown;
}

/**
 * 对应 C# 的 PromptOrderSetting，定义了某个角色的提示词顺序
 */
export interface PromptOrderSetting {
    character_id: number;
    order: OrderItem[];
}

/**
 * 根对象，代表一个完整的预设文件
 */
export interface SillyTavernPreset {
    prompts: PromptItem[];
    prompt_order: PromptOrderSetting[];
    [key: string]: unknown;
}

/**
 * 创建一个空的、有效的 SillyTavernPreset 对象
 */
export function createEmptyPreset(): SillyTavernPreset {
    return {
        prompts: [],
        prompt_order: [],
    };
}