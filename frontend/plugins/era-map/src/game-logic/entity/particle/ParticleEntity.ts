import type {IGameEntity} from '#/game-logic/entity/IGameEntity.ts';
import type {ParticleInfo} from '#/game-logic/entity/entityInfo.ts';
import {EntityInfoType} from '#/game-logic/entity/entityInfo.ts';
import type {ParticleLayerData} from '#/worldGeneration/types.ts';
import {Expose} from "class-transformer";

export class ParticleEntity implements IGameEntity
{
    @Expose()
    public readonly entityType: string = 'PARTICLE_ENTITY';
    @Expose()
    public readonly data: ParticleLayerData;

    constructor(data: ParticleLayerData)
    {
        // 我们不能在构造函数中访问data的属性，因为class-transformer会调用这个构造函数，然后传入undefined
        // 所以，我们把id和particleType从属性改为getter
        this.data = data;
    }

    public get id(): string
    {
        return this.data.type;
    }

    public get particleType(): string
    {
        return this.data.type;
    }

    public getInfoAt(gridX: number, gridY: number): ParticleInfo | null
    {
        const density = this.data.densityGrid[gridX]?.[gridY];
        if (density !== undefined && density > 0)
        {
            return {
                type: EntityInfoType.Particle,
                particleType: this.particleType,
                density: density,
            };
        }
        return null;
    }
}