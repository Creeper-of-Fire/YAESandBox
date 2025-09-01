// src/types/chat.ts
import type {Character, Scene} from '#/types/models';

/**
 * 消息的角色定义，遵循 OpenAI API 格式。
 */
export type MessageRole = 'User' | 'Assistant' | 'System';

/**
 * 聊天消息的载荷。这是整个设计的核心之一。
 * 它清晰地分离了“用于展示的内容”和“用于驱动逻辑的结构化数据”。
 */
export interface ChatMessagePayload
{
    /**
     * [给渲染器]
     * 用于最终展示的核心内容字符串，可能包含自定义XML标签。
     * 这是渲染的“唯一事实来源”。
     * 例如: "<character>RIM</character>ぷはー今日もイイ天気"
     */
    content: string;

    /**
     * [给游戏/应用逻辑]
     * 一个开放的容器，用于存放AI生成的、非对话性的结构化数据。
     * 系统可以根据这些数据更新状态、触发事件等。
     * 例如: { "variables": { "quest_status": "complete" }, "events": ["unlock_door"] }
     */
    structuredData?: Record<string, any>;
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
    data: ChatMessagePayload;
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