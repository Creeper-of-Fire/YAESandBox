/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AtomicOperationRequestDto } from './AtomicOperationRequestDto';
/**
 * (服务器 -> 客户端)
 * 当主工作流执行完成后，检测到 AI 生成的指令与用户在 Loading 状态下提交的指令存在冲突时发送。
 * 前端应使用此信息向用户展示冲突详情，并提供解决冲突的界面。
 */
export type ConflictDetectedDto = {
    /**
     * 发生冲突的 Block 的 ID。
     */
    blockId: string;
    /**
     * 关联的原始工作流请求 ID。
     */
    requestId: string;
    /**
     * 工作流（AI）生成的 **完整** 原子操作列表。
     */
    aiCommands: Array<AtomicOperationRequestDto>;
    /**
     * 用户在 Loading 期间提交的 **完整** 原子操作列表（可能包含因 Create/Create 冲突而被自动重命名的操作）。
     */
    userCommands: Array<AtomicOperationRequestDto>;
    /**
     * 导致 **阻塞性冲突** (Modify/Modify 同一属性) 的 AI 原子操作子集。
     */
    conflictingAiCommands: Array<AtomicOperationRequestDto>;
    /**
     * 导致 **阻塞性冲突** (Modify/Modify 同一属性) 的用户原子操作子集。
     */
    conflictingUserCommands: Array<AtomicOperationRequestDto>;
};

