import type { IGameEntity } from '#/game-logic/entity/IGameEntity.ts';
import type { ParticleInfo } from '#/game-logic/entity/entityInfo.ts';
import { EntityInfoType } from '#/game-logic/entity/entityInfo.ts';
import type { ParticleLayerData } from '#/game-resource/types.ts';

export class ParticleEntity implements IGameEntity {
    public readonly entityType: string = 'PARTICLE_ENTITY';
    public readonly id: string; // 就是 type
    public readonly particleType: string;
    public readonly data: ParticleLayerData;

    constructor(data: ParticleLayerData) {
        this.data = data;
        this.particleType = data.type;
        this.id = data.type;
    }

    public getInfoAt(gridX: number, gridY: number): ParticleInfo | null {
        const density = this.data.densityGrid[gridX]?.[gridY];
        if (density !== undefined && density > 0) {
            return {
                type: EntityInfoType.Particle,
                particleType: this.particleType,
                density: density,
            };
        }
        return null;
    }
}