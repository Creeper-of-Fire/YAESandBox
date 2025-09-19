import type {GameObjectEntity} from '#/game-logic/entity/gameObject/GameObjectEntity.ts';
import type {IGameEntity} from "#/game-logic/entity/IGameEntity.ts";
import {LogicalObjectLayer} from "#/game-logic/entity/gameObject/render/LogicalObjectLayer.ts";
import type {ILayer} from "#/game-logic/entity/ILayer.ts";
import type {IEntityContainer} from "#/game-logic/entity/IEntityContainer.ts";
import {Expose, Type} from "class-transformer";
import {TileMapLayer} from "#/game-resource/TileMapLayer.ts";
import {LayerType} from "#/game-logic/entity/LayerType.ts";
import {FieldContainerLayer} from "#/game-logic/entity/field/render/FieldContainerLayer.ts";
import {ParticleContainerLayer} from "#/game-logic/entity/particle/render/ParticleContainerLayer.ts";

// GameMap 是我们的世界状态的顶层容器
export class GameMap
{
    @Expose()
    public readonly gridWidth: number;
    @Expose()
    public readonly gridHeight: number;
    @Expose()
    @Type(() => Object, {
        // 关键：开启多态转换
        discriminator: {
            property: 'layerType', // 根据 'layerType' 属性来判断
            subTypes: [
                {value: TileMapLayer, name: LayerType.TileMapLayer},
                {value: LogicalObjectLayer, name: LayerType.LogicalObjectLayer},
                {value: FieldContainerLayer, name: LayerType.FieldContainerLayer},
                {value: ParticleContainerLayer, name: LayerType.ParticleContainerLayer},
            ],
        },
        // 保持类实例而不是纯对象
        keepDiscriminatorProperty: true,
    })
    public layers: ILayer[];

    constructor(
        gridWidth: number,
        gridHeight: number,
        layers: ILayer[],
    )
    {
        this.gridWidth = gridWidth;
        this.gridHeight = gridHeight;
        this.layers = layers;
    }

    // 我们可以添加一个辅助方法来方便地查找对象
    public findObjectById(id: string): GameObjectEntity | undefined
    {
        for (const layer of this.layers)
        {
            if (layer instanceof LogicalObjectLayer)
            {
                const found = layer.objects.find(obj => obj.id === id);
                if (found)
                {
                    return found;
                }
            }
        }
        return undefined;
    }

    /**
     * 在指定的网格位置获取所有实体。
     */
    public getEntitiesAtGridPosition(gridX: number, gridY: number): IGameEntity[]
    {
        const entities: IGameEntity[] = [];

        for (const layer of this.layers)
        {
            // 关键：我们只关心这个层是不是一个“实体容器”。
            // 我们使用 "in" 操作符进行类型守卫，这比 `instanceof` 更灵活。
            if ('getEntitiesAt' in layer)
            {
                const container = layer as ILayer & IEntityContainer;
                entities.push(...container.getEntitiesAt(gridX, gridY));
            }
        }
        return entities;
    }
}