// src/types/chat.ts
import type {Character, Scene} from '#/types/models';

/**
 * 消息的角色定义，遵循 OpenAI API 格式。
 */
export type MessageRole = 'User' | 'Assistant' | 'System';

/**
 * 消息内容的结构化载荷。
 * 存储所有与消息内容相关的数据。
 */
export interface MessagePayload
{
    content: string; // 核心的可视内容
    think?: string;  // AI 的思考过程
    [key: string]: any; // 扩展字段
}

/**
 * 聊天消息的数据结构。
 * 这是支持分支结构的核心：每条消息通过 `parentId` 指向上一个节点，
 * 形成一个非嵌套的树状结构。
 */
export interface ChatMessage
{
    id: string;
    sessionId: string;
    parentId: string | null; // null 表示这是根消息
    role: MessageRole;
    name?: string; // 可选，用于标识发言角色名
    content: MessagePayload;
    timestamp: number;
}

/**
 * 聊天会话的数据结构。
 * 每个会话记录了参与者、场景以及当前活跃的对话分支的“叶子节点”ID。
 */
export interface ChatSession
{
    id: string;
    title: string;
    playerCharacterId: string;
    targetCharacterId: string;
    sceneId: string;
    createdAt: number;
    // 跟踪当前对话分支的最后一个消息ID，以便继续对话或展示历史
    activeLeafMessageId: string;
}

// 用于在UI中展示的丰富会话信息
export interface EnrichedChatSession extends ChatSession
{
    playerCharacter?: Character;
    targetCharacter?: Character;
    scene?: Scene;
}