export interface IGameEntity {
    readonly id: string;
    readonly entityType: string; // e.g., 'LOGICAL_GAME_OBJECT', 'FIELD_ENTITY', 'PARTICLE_ENTITY'

    // 提供一个方法来获取其在逻辑网格上的包围盒
    getGridBoundingBox(): { x: number, y: number, width: number, height: number };
}