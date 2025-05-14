/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type {AtomicOperationRequestDto} from './AtomicOperationRequestDto';

/**
 * (客户端 -> 服务器)
 * 用于通过 SignalR 提交 **冲突解决方案** 的请求。
 * 当主工作流完成后检测到与用户修改冲突时，前端会收到 YAESandBox.API.DTOs.WebSocket.ConflictDetectedDto。
 * 用户解决冲突后，通过此 DTO 将最终确定的原子操作列表提交回后端。
 */
export type ResolveConflictRequestDto = {
    /**
     * 必须与导致冲突的原始工作流请求 (YAESandBox.API.DTOs.WebSocket.TriggerMainWorkflowRequestDto) 的 RequestId 相同，
     * 也应与收到的 YAESandBox.API.DTOs.WebSocket.ConflictDetectedDto 中的 RequestId 相同。
     * 用于将此解决方案关联回正确的冲突上下文。
     */
    requestId: string;
    /**
     * 发生冲突的 Block 的 ID (应与 YAESandBox.API.DTOs.WebSocket.ConflictDetectedDto 中的 BlockId 相同)。
     */
    blockId: string;
    /**
     * 经过用户确认或修改后的最终原子操作列表。
     * 这些操作将应用于 Block，以完成工作流并将其状态转换为 Idle (或 Error)。
     * 使用 YAESandBox.API.DTOs.AtomicOperationRequestDto 以便通过 SignalR 传输。
     */
    resolvedCommands: Array<AtomicOperationRequestDto>;
};

