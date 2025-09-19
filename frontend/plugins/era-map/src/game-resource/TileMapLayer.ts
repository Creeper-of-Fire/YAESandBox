import type {ILayer} from "#/game-logic/entity/ILayer.ts";
import {type Component, defineAsyncComponent} from "vue";

export class TileMapLayer implements ILayer
{
    public readonly tilesetId: string;
    public readonly data: number[][];

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