import type {GameObjectEntity} from '#/game-logic/entity/gameObject/GameObjectEntity.ts';
import type {IGameEntity} from "#/game-logic/entity/IGameEntity.ts";
import {LogicalObjectLayer} from "#/game-logic/entity/gameObject/render/LogicalObjectLayer.ts";
import type {ILayer} from "#/game-logic/entity/ILayer.ts";
import type {IEntityContainer} from "#/game-logic/entity/IEntityContainer.ts";

// GameMap 是我们的世界状态的顶层容器
export class GameMap
{
    public readonly gridWidth: number;
    public readonly gridHeight: number;
    // 注意：图层现在是混合类型的
    public readonly layers: ILayer[];

    constructor(config: {
        gridWidth: number;
        gridHeight: number;
        layers: ILayer[];
    })
    {
        this.gridWidth = config.gridWidth;
        this.gridHeight = config.gridHeight;
        this.layers = config.layers;
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
    public getEntitiesAtGridPosition(gridX: number, gridY: number): IGameEntity[] {
        const entities: IGameEntity[] = [];

        for (const layer of this.layers) {
            // 关键：我们只关心这个层是不是一个“实体容器”。
            // 我们使用 "in" 操作符进行类型守卫，这比 `instanceof` 更灵活。
            if ('getEntitiesAt' in layer) {
                const container = layer as ILayer & IEntityContainer;
                entities.push(...container.getEntitiesAt(gridX, gridY));
            }
        }
        return entities;
    }

    public toJSON() {
        return {
            gridWidth: this.gridWidth,
            gridHeight: this.gridHeight,
            layers: this.layers.map(layer => {
                // 每一层也需要 toJSON() 方法
                // @ts-ignore
                if (typeof layer.toJSON === 'function') {
                    // @ts-ignore
                    return layer.toJSON();
                }
                // 对于简单的层，可能直接返回其属性
                return layer;
            }),
        };
    }
}