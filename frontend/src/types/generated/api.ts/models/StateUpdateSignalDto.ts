/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { BlockDataFields } from './BlockDataFields';
/**
 * (服务器 -> 客户端)
 * 一个轻量级信号，通知客户端指定 Block 的状态 (WorldState 或 GameState) 可能已发生变化。
 * 鼓励客户端根据需要重新获取该 Block 的详细信息或相关实体的最新状态。
 */
export type StateUpdateSignalDto = {
    /**
     * 状态可能已发生变化的 Block 的 ID。
     */
    blockId?: string | null;
    /**
     * （可选）如果变化是由原子操作引起的，这里可以包含受影响的 Block 数据字段的枚举值，以便前端进行更精细的更新。如果为 null 或空，表示通用状态变更。
     */
    changedFields?: Array<BlockDataFields> | null;
    /**
     * （可选）如果变化是由原子操作引起的，这里可以包含受影响的实体的 ID 列表，以便前端进行更精细的更新。如果为 null 或空，表示未知具体实体。
     */
    changedEntityIds?: Array<string> | null;
};

