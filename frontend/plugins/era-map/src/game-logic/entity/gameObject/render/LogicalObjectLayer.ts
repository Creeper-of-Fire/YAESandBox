import type {ILayer} from "#/game-logic/entity/ILayer.ts";
import type {GameObjectEntity} from "#/game-logic/entity/gameObject/GameObjectEntity.ts";
import {type Component, defineAsyncComponent} from "vue";

// LogicalObjectLayer 只是一个简单的容器，持有 ObjectEntity 数组
export class LogicalObjectLayer implements ILayer
{

    public readonly objects: GameObjectEntity[];

    constructor(objects: GameObjectEntity[])
    {
        this.objects = objects;
    }

    public getRendererComponent(): Component
    {
        return defineAsyncComponent(() =>
            import('#/components/renderers/ObjectLayerRenderer.vue')
        );
    }
}