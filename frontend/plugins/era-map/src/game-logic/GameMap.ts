import type {GameObjectEntity} from '#/game-logic/entity/gameObject/GameObjectEntity.ts';
import type {IGameEntity} from "#/game-logic/entity/entity.ts";
import {LogicalObjectLayer} from "#/game-logic/entity/gameObject/render/LogicalObjectLayer.ts";
import type {ILayer} from "#/game-logic/entity/ILayer.ts";

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

    // TODO 现在问题很大，不包含其他类型，而且getGridBoundingBox感觉也不是最优解，应该还是让getGrid请求一个上下文进去，然后注册自身比较好？再看看
    public getEntitiesAtGridPosition(gridX: number, gridY: number): IGameEntity[]
    {
        const entities: IGameEntity[] = [];

        for (const layer of this.layers)
        {
            // 假设层本身可以被查询，或者我们检查层的内容
            // 这里我们假设 layer 包含一个 entities 数组
            if ('objects' in layer && Array.isArray(layer.objects))
            {
                for (const entity of (layer as LogicalObjectLayer).objects)
                {
                    const box = entity.getGridBoundingBox();
                    if (
                        gridX >= box.x && gridX < box.x + box.width &&
                        gridY >= box.y && gridY < box.y + box.height
                    )
                    {
                        entities.push(entity);
                    }
                }
            }
            // 未来可以扩展到 FieldEntity, ParticleEntity 等
        }
        return entities;
    }
}