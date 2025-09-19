import type { TileLayer, FieldLayer, ParticleLayer } from '#/game-render/GameMap';
import type { LogicalGameObject } from '#/game-logic/LogicalGameObject';

// LogicalObjectLayer 只是一个简单的容器，持有 LogicalGameObject 数组
export class LogicalObjectLayer {

    public readonly objects: LogicalGameObject[];

    constructor(objects: LogicalGameObject[]) {
        this.objects = objects;
    }
}

// LogicalGameMap 是我们新的世界状态的顶层容器
export class LogicalGameMap {
    public readonly gridWidth: number;
    public readonly gridHeight: number;
    // 注意：图层现在是混合类型的
    public readonly layers: (TileLayer | LogicalObjectLayer | FieldLayer | ParticleLayer)[];

    constructor(config: {
        gridWidth: number;
        gridHeight: number;
        layers: (TileLayer | LogicalObjectLayer | FieldLayer | ParticleLayer)[];
    }) {
        this.gridWidth = config.gridWidth;
        this.gridHeight = config.gridHeight;
        this.layers = config.layers;
    }

    // 我们可以添加一个辅助方法来方便地查找对象
    public findObjectById(id: string): LogicalGameObject | undefined {
        for (const layer of this.layers) {
            if (layer instanceof LogicalObjectLayer) {
                const found = layer.objects.find(obj => obj.id === id);
                if (found) {
                    return found;
                }
            }
        }
        return undefined;
    }
}