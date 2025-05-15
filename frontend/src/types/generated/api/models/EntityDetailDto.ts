/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { EntityType } from './EntityType';
/**
 * 用于 API 响应，表示实体的详细信息，包含所有属性。
 * 继承自 YAESandBox.API.DTOs.EntitySummaryDto。
 */
export type EntityDetailDto = {
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
    /**
     * 包含实体所有属性（包括核心属性如 IsDestroyed 和动态属性）的字典。
     * 值的类型可能是 string, int, bool, double, List[object?], Dictionary-[string, object?], TypedID 等。
     */
    attributes: Record<string, any>;
};

