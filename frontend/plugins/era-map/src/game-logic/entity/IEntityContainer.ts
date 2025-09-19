import type {IGameEntity} from "#/game-logic/entity/IGameEntity.ts";

/**
 * 【新接口】代表一个可以容纳和查询实体的容器。
 * 这是一个“混合（mixin）”接口，通常由一个 ILayer 来实现。
 */
export interface IEntityContainer
{
    /**
     * 在指定的网格坐标查询所有相关的实体。
     * @returns 一个 IGameEntity 数组。
     */
    getEntitiesAt(gridX: number, gridY: number): IGameEntity[];
}