import { type Component, defineAsyncComponent } from "vue";
import type { ILayer } from "#/game-logic/entity/ILayer.ts";
import type { IEntityContainer } from "#/game-logic/entity/IEntityContainer.ts";
import type { IGameEntity } from "#/game-logic/entity/IGameEntity.ts";
import type { FieldEntity } from "#/game-logic/entity/field/FieldEntity.ts";

export class FieldContainerLayer implements ILayer, IEntityContainer {
    public readonly entities: FieldEntity[];

    constructor(entities: FieldEntity[]) {
        this.entities = entities;
    }

    public getEntitiesAt(gridX: number, gridY: number): IGameEntity[] {
        // 场实体总是覆盖整个地图，所以直接返回所有实体
        return this.entities;
    }

    public getRendererComponent(): Component {
        return defineAsyncComponent(() =>
            import('#/components/renderers/FieldLayerRenderer.vue')
        );
    }
}