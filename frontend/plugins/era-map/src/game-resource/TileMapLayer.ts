import type {ILayer} from "#/game-logic/entity/ILayer.ts";
import {type Component, defineAsyncComponent} from "vue";
import {Expose} from 'class-transformer';
import {LayerType} from "#/game-logic/entity/LayerType.ts";

export class TileMapLayer implements ILayer
{
    @Expose()
    public readonly tilesetId: string;
    @Expose()
    public readonly data: number[][];
    @Expose()
    public readonly layerType = LayerType.TileMapLayer;

    constructor(config: { tilesetId: string; data: number[][] })
    {
        this.tilesetId = config.tilesetId;
        this.data = config.data;
    }

    public getRendererComponent(): Component
    {
        return defineAsyncComponent(() =>
            import('#/components/renderers/TileLayerRenderer.vue')
        );
    }
}