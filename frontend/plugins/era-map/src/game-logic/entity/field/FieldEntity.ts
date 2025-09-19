import type {IGameEntity} from '#/game-logic/entity/IGameEntity.ts';
import type {FieldInfo} from '#/game-logic/entity/entityInfo.ts';
import {EntityInfoType} from '#/game-logic/entity/entityInfo.ts';
import {Expose} from "class-transformer";

export class FieldEntity implements IGameEntity
{
    @Expose()
    public readonly entityType: string = 'FIELD_ENTITY';
    @Expose()
    public readonly id: string; // 就是 name
    @Expose()
    public readonly name: string;
    @Expose()
    public readonly data: number[][];

    constructor(config: { name: string; data: number[][] })
    {
        this.name = config.name;
        this.id = config.name;
        this.data = config.data;
    }

    public getInfoAt(gridX: number, gridY: number): FieldInfo | null
    {
        const value = this.data[gridX]?.[gridY];
        if (value !== undefined)
        {
            return {
                type: EntityInfoType.Field,
                name: this.name,
                value: value,
            };
        }
        return null;
    }
}