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
    public readonly id: string; // 就是 type
    @Expose()
    public readonly particleType: string;
    @Expose()
    public readonly data: ParticleLayerData;

    constructor(data: ParticleLayerData)
    {
        this.data = data;
        this.particleType = data.type;
        this.id = data.type;
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