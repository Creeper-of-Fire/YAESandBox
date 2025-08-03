/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { EntityType } from './EntityType';
/**
 * 用于 API 请求的单个原子操作的表示。
 * 定义了对 WorldState 中实体的创建、修改或删除操作。
 */
export type AtomicOperationRequestDto = {
    /**
     * 操作类型。必须是 "CreateEntity", "ModifyEntity", 或 "DeleteEntity" (不区分大小写)。
     */
    operationType: string;
    entityType: EntityType;
    /**
     * 操作目标实体的唯一 ID。不能为空或仅包含空白字符。
     */
    entityId: string;
    /**
     * (仅用于 CreateEntity 操作)
     * 要创建的实体的初始属性字典。键是属性名，值是属性值。
     * 如果不提供，则实体以默认属性创建。
     */
    initialAttributes?: Record<string, any> | null;
    /**
     * (仅用于 ModifyEntity 操作)
     * 要修改的属性的键（名称）。
     */
    attributeKey?: string | null;
    /**
     * (仅用于 ModifyEntity 操作)
     * 修改操作符。预期值为 "=", "+=", "-=" 等表示赋值、增加、减少等操作的字符串。
     * 具体支持的操作符取决于后端实现 (YAESandBox.Seed.State.Entity.OperatorHelper)。
     */
    modifyOperator?: string | null;
    /**
     * (仅用于 ModifyEntity 操作)
     * 修改操作的值。类型应与目标属性和操作符兼容。
     */
    modifyValue?: any;
};

