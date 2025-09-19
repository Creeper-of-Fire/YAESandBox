import type {ParticleLayerData} from "#/game-resource/types.ts";
import {type Component, defineAsyncComponent} from "vue";

import type {ILayer} from "#/game-logic/entity/ILayer.ts";

export class ParticleLayer implements ILayer
{
    public readonly data: ParticleLayerData;

    constructor(data: ParticleLayerData)
    {
        this.data = data;
    }

    public getRendererComponent(): Component
    {
        return defineAsyncComponent(() =>
            import('#/components/renderers/ParticleLayerRenderer.vue')
        );
    }
}