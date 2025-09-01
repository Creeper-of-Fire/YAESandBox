// src/features/chat/chatStore.ts
import {defineStore} from 'pinia';
import {computed, toRaw} from 'vue';
import {nanoid} from 'nanoid';
import {createPersistentState} from '#/composables/createPersistentState.ts';
import type {ChatMessage, ChatSession, EnrichedChatSession, ChatMessagePayload} from '#/types/chat';
import {useCharacterStore} from '#/features/characters/characterStore.ts';
import {useSceneStore} from '#/features/scenes/sceneStore.ts';

const STORAGE_KEY_SESSIONS = 'era-lite-chat-sessions';
const STORAGE_KEY_MESSAGES = 'era-lite-chat-messages';

export const useChatStore = defineStore('era-lite-chat', () =>
{
    const {state: sessions, isReady: isSessionsReady} = createPersistentState<ChatSession[]>(STORAGE_KEY_SESSIONS, []);
    // 消息以 Record<id, message> 的形式存储，便于快速查找，避免深层嵌套
    const {state: messages, isReady: isMessagesReady} = createPersistentState<Record<string, ChatMessage>>(STORAGE_KEY_MESSAGES, {});

    const characterStore = useCharacterStore();
    const sceneStore = useSceneStore();

    // --- Getters (Computed) ---

    const isReady = computed(() => isSessionsReady.value && isMessagesReady.value);

    const enrichedSessions = computed((): EnrichedChatSession[] =>
    {
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
    function getHistoryFromLeaf(leafMessageId: string): ChatMessage[]
    {
        const history: ChatMessage[] = [];
        let currentId: string | null = leafMessageId;

        while (currentId)
        {
            const message: ChatMessage = messages.value[currentId];
            if (message)
            {
                history.unshift(message); // 插入到数组开头以保持顺序
                currentId = message.parentId;
            }
            else
            {
                break; // 找不到父消息，终止循环
            }
        }
        return history;
    }

    /**
     * 查找指定消息的所有兄弟节点（包括其自身）。
     * @param messageId - 消息ID。
     * @returns 兄弟消息数组，按时间戳排序。
     */
    function findMessageSiblings(messageId: string): ChatMessage[]
    {
        const message = messages.value[messageId];
        if (!message || !message.parentId) return [message].filter(Boolean);

        const parentId = message.parentId;
        const siblings: ChatMessage[] = [];
        for (const id in messages.value)
        {
            if (messages.value[id].parentId === parentId)
            {
                siblings.push(messages.value[id]);
            }
        }
        return siblings.sort((a, b) => a.timestamp - b.timestamp);
    }

    // --- Actions ---

    /**
     * 创建一个新的聊天会话。
     * @param playerCharacterId - 主角ID
     * @param targetCharacterId - 交互对象ID
     * @param sceneId - 场景ID
     * @returns 新创建的会话ID。
     */
    function createChatSession(playerCharacterId: string, targetCharacterId: string, sceneId: string): string
    {
        const playerCharacter = characterStore.characters.find(c => c.id === playerCharacterId);
        const targetCharacter = characterStore.characters.find(c => c.id === targetCharacterId);
        const scene = sceneStore.scenes.find(s => s.id === sceneId);

        if (!playerCharacter || !targetCharacter || !scene)
        {
            throw new Error('Invalid participants or scene for chat session.');
        }

        const sessionId = nanoid();
        const rootMessageId = nanoid();

        const rootMessage: ChatMessage = {
            id: rootMessageId,
            sessionId: sessionId,
            parentId: null,
            role: 'System',
            data: {content: ''},
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
    function createAssistantMessagePlaceholder(sessionId: string, parentId: string): string
    {
        const session = sessions.value.find(s => s.id === sessionId);
        if (!session) throw new Error("Session not found");

        const assistantMessageId = nanoid();
        const assistantMessage: ChatMessage = {
            id: assistantMessageId,
            sessionId: sessionId,
            parentId: parentId,
            role: 'Assistant',
            name: characterStore.characters.find(c => c.id === session.targetCharacterId)?.name,
            data: {content: ''}, // 内容为空
            timestamp: Date.now(),
        };
        messages.value[assistantMessageId] = assistantMessage;

        // 立即更新活动分支的叶子节点为这个新的占位符
        session.activeLeafMessageId = assistantMessageId;

        return assistantMessageId;
    }

    /**
     * 更新指定ID消息的数据。
     * @param messageId - 要更新的消息ID。
     * @param newPayload - 最新的部分或完整数据。
     */
    function updateMessageData(messageId: string, newPayload: Partial<ChatMessagePayload>)
    {
        const message = messages.value[messageId];
        if (message)
        {
            // 合并新旧载荷
            message.data = {...toRaw(message.data), ...newPayload};
        }
    }

    /**
     * 更新指定ID消息的内容。主要用于用户编辑。
     * @param messageId - 要更新的消息ID。
     * @param newContent - 新的文本内容。
     */
    function editMessageContent(messageId: string, newContent: string) {
        const message = messages.value[messageId];
        if (message) {
            message.data.content = newContent;
            // 编辑会使所有后续分支无效，因此删除所有子消息。
            deleteMessageAndChildren(messageId, true);

            // 明确地将此编辑过的消息设置为当前分支的末端。
            setActiveLeaf(message.sessionId, messageId);
        }
    }

    /**
     * 删除指定消息及其所有后代消息。
     * @param messageId - 要删除的起始消息ID。
     * @param keepSelf - 是否保留 messageId 本身（用于编辑后删除子节点）。
     */
    function deleteMessageAndChildren(messageId: string, keepSelf = false) {
        // 1. 在删除前，获取所有必要信息
        const startingMessage = messages.value[messageId];
        if (!startingMessage) {
            console.warn(`Attempted to delete a non-existent message: ${messageId}`);
            return;
        }
        const { sessionId, parentId } = startingMessage;

        // 2. 广度优先搜索（BFS）找到所有要删除的后代消息ID
        const toDelete = new Set<string>();
        const queue: string[] = [messageId];

        if (!keepSelf) {
            toDelete.add(messageId);
        }

        let head = 0;
        while(head < queue.length) {
            const currentId = queue[head++]; // 使用索引代替 shift() 以提高性能
            for (const id in messages.value) {
                if (messages.value[id].parentId === currentId) {
                    toDelete.add(id);
                    queue.push(id);
                }
            }
        }

        // 3. 检查当前激活的对话分支是否会受到影响
        const session = sessions.value.find(s => s.id === sessionId);
        if (!session) return;

        // 如果激活分支的叶子节点在即将被删除的集合中，说明分支受到了影响
        const activeBranchIsAffected = toDelete.has(session.activeLeafMessageId);

        // 4. 执行删除操作
        toDelete.forEach(id => {
            delete messages.value[id];
        });

        // 5. 如果激活分支被影响，将其安全地重置到被删除节点的父节点上
        if (activeBranchIsAffected) {
            if (parentId && messages.value[parentId]) {
                session.activeLeafMessageId = parentId;
            } else {
                // 如果父节点也找不到了（极端情况），回退到会话的根消息
                const rootMessage = Object.values(messages.value).find(m => m.sessionId === sessionId && m.parentId === null);
                if (rootMessage) {
                    session.activeLeafMessageId = rootMessage.id;
                }
            }
        }
    }

    /**
     * 设置会话的当前激活分支。
     * @param sessionId - 会话ID。
     * @param leafMessageId - 新分支的叶子消息ID。
     */
    function setActiveLeaf(sessionId: string, leafMessageId: string)
    {
        const session = sessions.value.find(s => s.id === sessionId);
        if (session && messages.value[leafMessageId])
        {
            session.activeLeafMessageId = leafMessageId;
        }
    }

    /**
     * 向指定会话添加一条用户消息。
     * 这个函数只处理用户消息，不触发AI响应。
     * @param sessionId - 会话ID。
     * @param userInput - 用户的输入内容。
     * @returns 新创建的用户消息。
     */
    function addUserMessage(sessionId: string, userInput: string): ChatMessage
    {
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
            data: {content: userInput},
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
        for (const messageId in messages.value)
        {
            if (messages.value[messageId].sessionId === sessionId)
            {
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
        updateMessageData,
        findMessageSiblings,
        deleteMessageAndChildren,
        editMessageContent,
        setActiveLeaf,
    };
});