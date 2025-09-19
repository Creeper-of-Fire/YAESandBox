import type {ILayer} from "#/game-logic/entity/ILayer.ts";
import  {type GameObjectEntity} from "#/game-logic/entity/gameObject/GameObjectEntity.ts";
import {type Component, defineAsyncComponent} from "vue";
import type {IEntityContainer} from "#/game-logic/entity/IEntityContainer.ts";
import type {IGameEntity} from "#/game-logic/entity/IGameEntity.ts";


export class LogicalObjectLayer implements ILayer,IEntityContainer
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

    public getEntitiesAt(gridX: number, gridY: number): IGameEntity[] {
        return this.objects.filter(obj => obj.getInfoAt(gridX, gridY) !== null);
    }
}