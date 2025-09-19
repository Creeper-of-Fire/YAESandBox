import {type Component, defineAsyncComponent} from "vue";

import type {ILayer} from "#/game-logic/entity/ILayer.ts";

export class FieldLayer implements ILayer
{
    public readonly data: number[][];
    public readonly name: string; // e.g., 'light_level'

    constructor(config: { name: string; data: number[][] })
    {
        this.name = config.name;
        this.data = config.data;
    }

    public getRendererComponent(): Component
    {
        return defineAsyncComponent(() =>
            import('#/components/renderers/FieldLayerRenderer.vue')
        );
    }
}