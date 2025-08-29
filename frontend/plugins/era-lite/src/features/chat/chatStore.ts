// src/features/chat/chatStore.ts
import { defineStore } from 'pinia';
import { computed } from 'vue';
import { nanoid } from 'nanoid';
import { createPersistentState } from '#/composables/createPersistentState.ts';
import type {ChatMessage, ChatSession, EnrichedChatSession, MessagePayload} from '#/types/chat';
import { useCharacterStore } from '#/features/characters/characterStore.ts';
import { useSceneStore } from '#/features/scenes/sceneStore.ts';

const STORAGE_KEY_SESSIONS = 'era-lite-chat-sessions';
const STORAGE_KEY_MESSAGES = 'era-lite-chat-messages';

export const useChatStore = defineStore('era-lite-chat', () => {
    const { state: sessions, isReady: isSessionsReady } = createPersistentState<ChatSession[]>(STORAGE_KEY_SESSIONS, []);
    // 消息以 Record<id, message> 的形式存储，便于快速查找，避免深层嵌套
    const { state: messages, isReady: isMessagesReady } = createPersistentState<Record<string, ChatMessage>>(STORAGE_KEY_MESSAGES, {});

    const characterStore = useCharacterStore();
    const sceneStore = useSceneStore();

    // --- Getters (Computed) ---

    const isReady = computed(() => isSessionsReady.value && isMessagesReady.value);

    const enrichedSessions = computed((): EnrichedChatSession[] => {
        return sessions.value
            .map(session => ({
                ...session,
                playerCharacter: characterStore.characters.find(c => c.id === session.playerCharacterId),
                targetCharacter: characterStore.characters.find(c => c.id === session.targetCharacterId),
                scene: sceneStore.scenes.find(s => s.id === session.sceneId),
            }))
            .sort((a, b) => b.createdAt - a.createdAt); // 按创建时间降序排序
    });

    /**
     * 根据叶子消息ID，向上追溯，构建出完整的对话历史记录。
     * 这是实现分支对话展示的核心。
     * @param leafMessageId - 对话分支的最后一条消息的ID。
     * @returns 一个按时间顺序排列的消息数组。
     */
    function getHistoryFromLeaf(leafMessageId: string): ChatMessage[] {
        const history: ChatMessage[] = [];
        let currentId: string | null = leafMessageId;

        while (currentId) {
            const message: ChatMessage = messages.value[currentId];
            if (message) {
                history.unshift(message); // 插入到数组开头以保持顺序
                currentId = message.parentId;
            } else {
                break; // 找不到父消息，终止循环
            }
        }
        return history;
    }

    // --- Actions ---

    /**
     * 创建一个新的聊天会话。
     * @param playerCharacterId - 主角ID
     * @param targetCharacterId - 交互对象ID
     * @param sceneId - 场景ID
     * @returns 新创建的会话ID。
     */
    function createChatSession(playerCharacterId: string, targetCharacterId: string, sceneId: string): string {
        const playerCharacter = characterStore.characters.find(c => c.id === playerCharacterId);
        const targetCharacter = characterStore.characters.find(c => c.id === targetCharacterId);
        const scene = sceneStore.scenes.find(s => s.id === sceneId);

        if (!playerCharacter || !targetCharacter || !scene) {
            throw new Error('Invalid participants or scene for chat session.');
        }

        const sessionId = nanoid();
        const rootMessageId = nanoid();

        const rootMessage: ChatMessage = {
            id: rootMessageId,
            sessionId: sessionId,
            parentId: null,
            role: 'System',
            content: { content: '' },
            timestamp: Date.now(),
        };
        messages.value[rootMessageId] = rootMessage;

        const newSession: ChatSession = {
            id: sessionId,
            title: `与 ${targetCharacter.name} 的对话`,
            playerCharacterId,
            targetCharacterId,
            sceneId,
            createdAt: Date.now(),
            activeLeafMessageId: rootMessageId,
        };

        sessions.value.unshift(newSession); // 新会话放在最前面
        return sessionId;
    }

    /**
     * 创建一个新的AI消息占位符。
     * @param sessionId - 所属会话ID。
     * @param parentId - 父消息（通常是用户消息）的ID。
     * @returns 新创建的占位符消息的ID。
     */
    function createAssistantMessagePlaceholder(sessionId: string, parentId: string): string {
        const session = sessions.value.find(s => s.id === sessionId);
        if (!session) throw new Error("Session not found");

        const assistantMessageId = nanoid();
        const assistantMessage: ChatMessage = {
            id: assistantMessageId,
            sessionId: sessionId,
            parentId: parentId,
            role: 'Assistant',
            name: characterStore.characters.find(c => c.id === session.targetCharacterId)?.name,
            content: { content: '' }, // 内容为空
            timestamp: Date.now(),
        };
        messages.value[assistantMessageId] = assistantMessage;

        // 立即更新活动分支的叶子节点为这个新的占位符
        session.activeLeafMessageId = assistantMessageId;

        return assistantMessageId;
    }

    /**
     * 更新指定ID消息的载荷。
     * @param messageId - 要更新的消息ID。
     * @param newPayload - 最新的部分或完整载荷。
     */
    function updateMessagePayload(messageId: string, newPayload: Partial<MessagePayload>) {
        const message = messages.value[messageId];
        if (message) {
            // 合并新旧载荷，以支持流式地更新 content 和 think
            message.content = { ...message.content, ...newPayload };
        }
    }

    /**
     * 向指定会话添加一条用户消息。
     * 这个函数只处理用户消息，不触发AI响应。
     * @param sessionId - 会话ID。
     * @param userInput - 用户的输入内容。
     * @returns 新创建的用户消息。
     */
    function addUserMessage(sessionId: string, userInput: string): ChatMessage {
        const session = sessions.value.find(s => s.id === sessionId);
        if (!session) throw new Error("Session not found");

        const playerCharacter = characterStore.characters.find(c => c.id === session.playerCharacterId);
        if (!playerCharacter) throw new Error("PlayerCharacter not found");

        const userMessageId = nanoid();
        const userMessage: ChatMessage = {
            id: userMessageId,
            sessionId: sessionId,
            parentId: session.activeLeafMessageId,
            role: 'User',
            name: playerCharacter.name,
            content: { content: userInput },
            timestamp: Date.now(),
        };
        messages.value[userMessageId] = userMessage;
        session.activeLeafMessageId = userMessageId; // 更新活动分支

        return userMessage;
    }

    function deleteSession(sessionId: string)
    {
        sessions.value = sessions.value.filter(s => s.id !== sessionId);
        // 删除该会话下的所有消息
        for (const messageId in messages.value) {
            if (messages.value[messageId].sessionId === sessionId) {
                delete messages.value[messageId];
            }
        }
    }

    return {
        sessions,
        messages,
        isReady,
        enrichedSessions,
        createChatSession,
        getHistoryFromLeaf,
        deleteSession,
        addUserMessage,
        createAssistantMessagePlaceholder,
        updateMessagePayload,
    };
});