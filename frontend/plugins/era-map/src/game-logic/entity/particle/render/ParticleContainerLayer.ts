import { type Component, defineAsyncComponent } from "vue";
import type { ILayer } from "#/game-logic/entity/ILayer.ts";
import type { IEntityContainer } from "#/game-logic/entity/IEntityContainer.ts";
import type { IGameEntity } from "#/game-logic/entity/IGameEntity.ts";
import type { ParticleEntity } from '#/game-logic/entity/particle/ParticleEntity.ts';

export class ParticleContainerLayer implements ILayer, IEntityContainer {
    public readonly entities: ParticleEntity[];

    constructor(entities: ParticleEntity[]) {
        this.entities = entities;
    }

    public getEntitiesAt(gridX: number, gridY: number): IGameEntity[] {
        return this.entities;
    }

    public getRendererComponent(): Component {
        return defineAsyncComponent(() =>
            import('#/components/renderers/ParticleLayerRenderer.vue')
        );
    }
}