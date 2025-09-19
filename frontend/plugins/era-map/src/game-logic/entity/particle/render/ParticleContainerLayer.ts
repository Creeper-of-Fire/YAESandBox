import {type Component, defineAsyncComponent} from "vue";
import type {ILayer} from "#/game-logic/entity/ILayer.ts";
import type {IEntityContainer} from "#/game-logic/entity/IEntityContainer.ts";
import type {IGameEntity} from "#/game-logic/entity/IGameEntity.ts";
import {ParticleEntity} from '#/game-logic/entity/particle/ParticleEntity.ts';
import {Expose, Type} from "class-transformer";
import {FieldContainerLayer} from "#/game-logic/entity/field/render/FieldContainerLayer.ts";
import {TileMapLayer} from "#/game-resource/TileMapLayer.ts";
import {LogicalObjectLayer} from "#/game-logic/entity/gameObject/render/LogicalObjectLayer.ts";
import {LayerType} from "#/game-logic/entity/LayerType.ts";

export class ParticleContainerLayer implements ILayer, IEntityContainer
{
    @Expose()
    @Type(() => ParticleEntity)
    public readonly entities: ParticleEntity[];
    @Expose()
    public readonly layerType = LayerType.ParticleContainerLayer;

    constructor(entities: ParticleEntity[])
    {
        this.entities = entities;
    }

    public getEntitiesAt(gridX: number, gridY: number): IGameEntity[]
    {
        return this.entities;
    }

    public getRendererComponent(): Component
    {
        return defineAsyncComponent(() =>
            import('#/components/renderers/ParticleLayerRenderer.vue')
        );
    }
}