/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { StreamStatus } from './StreamStatus';
import type { UpdateMode } from './UpdateMode';
/**
 * (服务器 -> 客户端)
 * 携带由工作流（主工作流或微工作流）生成或处理的内容，用于更新前端显示。
 */
export type DisplayUpdateDto = {
    /**
     * 关联的原始工作流请求 ID (YAESandBox.API.DTOs.WebSocket.TriggerMainWorkflowRequestDto.RequestId 或 YAESandBox.API.DTOs.WebSocket.TriggerMicroWorkflowRequestDto.RequestId)。
     */
    requestId: string;
    /**
     * 主要关联的 Block ID。对于主工作流，这是被更新或新创建的 Block。对于微工作流，这通常是触发时的上下文 Block。
     */
    contextBlockId: string;
    /**
     * 需要显示或处理的内容字符串。可能是完整的文本、HTML 片段、JSON 数据，或增量更新指令。
     */
    content: string;
    streamingStatus: StreamStatus;
    updateMode: UpdateMode;
    /**
     * (关键区分) 目标 UI 元素或逻辑区域的 ID。
     * - 如果为 **null** 或空字符串：表示这是一个 **主工作流** 更新，应更新与 <cref name="ContextBlockId" /> 关联的主要显示区域。
     * - 如果 **非 null**：表示这是一个 **微工作流** 更新，应更新 ID 与此值匹配的特定 UI 元素或区域。
     */
    targetElementId?: string | null;
    /**
     * (可选，仅当 <cref name="UpdateMode" /> 为 YAESandBox.API.DTOs.WebSocket.UpdateMode.Incremental 时相关)
     * 指示增量更新的类型，例如 "JsonPatch", "DiffMatchPatch", "SimpleAppend" 等。
     * 具体值和解释取决于前后端约定。
     */
    incrementalType?: string | null;
    /**
     * (可选) 消息的序列号，用于处理乱序或重复的消息。
     * 客户端可以根据需要实现排序和去重逻辑。
     */
    sequenceNumber?: number | null;
};

