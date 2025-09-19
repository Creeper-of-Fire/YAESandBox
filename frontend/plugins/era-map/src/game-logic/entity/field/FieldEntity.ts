import type {IGameEntity} from '#/game-logic/entity/IGameEntity.ts';
import type {FieldInfo} from '#/game-logic/entity/entityInfo.ts';
import {EntityInfoType} from '#/game-logic/entity/entityInfo.ts';
import {Expose} from "class-transformer";

export class FieldEntity implements IGameEntity
{
    @Expose()
    public readonly entityType: string = 'FIELD_ENTITY';
    @Expose()
    public readonly id: string;
    @Expose()
    public readonly name: string;
    @Expose()
    public readonly data: number[][];

    constructor(name: string, data: number[][])
    {
        this.name = name;
        this.id = name;
        this.data = data;
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