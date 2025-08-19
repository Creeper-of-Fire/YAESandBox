// services/eventBus.ts
import mitt from 'mitt';
import {StreamStatus} from "#/app-game/types/generated/public-api-client";

// 定义事件类型 (关键是 targetElementId)
type Events = {
    // 事件名格式: `microWorkflowUpdate:${targetElementId}`
    [key: `microWorkflowUpdate:${string}`]: { // 使用模板字面量类型
        content: string | null;
        status: StreamStatus; // 引入 StreamStatus 类型
        updateMode: string; // 或 UpdateMode 类型
        // 可以添加 requestId 等其他需要的信息
    };
    // 事件名格式: `${contextBlockId}:GameStateChanged`
    [key: `${string}:GameStateChanged`]: {};
    // 事件名格式: `${contextBlockId}:WorldStateChanged`
    [key: `${string}:WorldStateChanged`]: {};
};

export const eventBus = mitt<Events>();