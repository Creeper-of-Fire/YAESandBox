/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { EntityType } from './EntityType';
/**
 * 用于 API 响应，表示实体的基本摘要信息。
 */
export type EntitySummaryDto = {
    /**
     * 实体的唯一 ID。
     */
    entityId: string;
    entityType: EntityType;
    /**
     * 指示实体是否已被标记为销毁。
     * 注意：查询 API 通常只返回未销毁的实体。
     */
    isDestroyed: boolean;
    /**
     * 实体的名称 (通常来自 'name' 属性，如果不存在则可能回退到 EntityId)。
     */
    name?: string | null;
};

