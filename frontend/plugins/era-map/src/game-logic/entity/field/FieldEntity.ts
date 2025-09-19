import type { IGameEntity } from '#/game-logic/entity/IGameEntity.ts';
import type { EntityInfo, FieldInfo } from '#/game-logic/entity/entityInfo.ts';
import { EntityInfoType } from '#/game-logic/entity/entityInfo.ts';

export class FieldEntity implements IGameEntity {
    public readonly entityType: string = 'FIELD_ENTITY';
    public readonly id: string; // 就是 name
    public readonly name: string;
    public readonly data: number[][];

    constructor(config: { name: string; data: number[][] }) {
        this.name = config.name;
        this.id = config.name;
        this.data = config.data;
    }

    public getInfoAt(gridX: number, gridY: number): FieldInfo | null {
        const value = this.data[gridX]?.[gridY];
        if (value !== undefined) {
            return {
                type: EntityInfoType.Field,
                name: this.name,
                value: value,
            };
        }
        return null;
    }
}