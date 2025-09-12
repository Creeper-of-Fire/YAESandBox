import type { GameObject } from './GameObject';
import { defineAsyncComponent, type Component } from 'vue';
import type { FieldLayerData, ParticleLayerData } from './types';

export interface ILayer {
    // 这个方法是核心：它返回一个可被Vue渲染的组件
    getRendererComponent(): Component;
}

export class TileLayer implements ILayer {
    public readonly tilesetId: string;
    public readonly data: number[][];

    constructor(config: { tilesetId: string; data: number[][] }) {
        this.tilesetId = config.tilesetId;
        this.data = config.data;
    }

    public getRendererComponent(): Component {
        return defineAsyncComponent(() =>
            import('#/components/renderers/TileLayerRenderer.vue')
        );
    }
}

export class FieldLayer implements ILayer {
    public readonly data: number[][];
    public readonly name: string; // e.g., 'light_level'

    constructor(config: { name: string; data: number[][] }) {
        this.name = config.name;
        this.data = config.data;
    }

    public getRendererComponent(): Component {
        return defineAsyncComponent(() =>
            import('#/components/renderers/FieldLayerRenderer.vue')
        );
    }
}

export class ParticleLayer implements ILayer {
    public readonly data: ParticleLayerData;

    constructor(data: ParticleLayerData) {
        this.data = data;
    }

    public getRendererComponent(): Component {
        return defineAsyncComponent(() =>
            import('#/components/renderers/ParticleLayerRenderer.vue')
        );
    }
}

export class ObjectLayer implements ILayer {
    public readonly objects: GameObject[];

    constructor(objects: GameObject[]) {
        this.objects = objects;
    }

    public getRendererComponent(): Component {
        return defineAsyncComponent(() =>
            import('#/components/renderers/ObjectLayerRenderer.vue')
        );
    }
}

export interface CellData {
    objects: GameObject[];
    fields: { name: string; value: number }[];
    particles: { type: string; count: number }[];
}

export class GameMap {
    public readonly gridWidth: number;
    public readonly gridHeight: number;
    public readonly layers: (TileLayer | ObjectLayer | FieldLayer | ParticleLayer)[];

    constructor(config: {
        gridWidth: number;
        gridHeight: number;
        layers: (TileLayer | ObjectLayer | FieldLayer | ParticleLayer)[];
    }) {
        this.gridWidth = config.gridWidth;
        this.gridHeight = config.gridHeight;
        this.layers = config.layers;
    }

    /**
     * 收集并返回指定网格坐标内的所有相关数据。
     * @param gridX - 网格的X坐标
     * @param gridY - 网格的Y坐标
     * @returns 一个包含对象、场和粒子信息的对象
     */
    public getDataAtGridPosition(gridX: number, gridY: number): CellData {
        const result: CellData = {
            objects: [],
            fields: [],
            particles: [],
        };

        for (const layer of this.layers) {
            if (layer instanceof ObjectLayer) {
                for (const obj of layer.objects) {
                    const size = obj.config.gridSize || { width: 1, height: 1 };
                    // 检查该格子是否在对象的占地范围内
                    if (
                        gridX >= obj.gridPosition.x && gridX < obj.gridPosition.x + size.width &&
                        gridY >= obj.gridPosition.y && gridY < obj.gridPosition.y + size.height
                    ) {
                        result.objects.push(obj);
                    }
                }
            } else if (layer instanceof FieldLayer) {
                const value = layer.data[gridX]?.[gridY];
                if (value !== undefined) {
                    result.fields.push({ name: layer.name, value });
                }
            } else if (layer instanceof ParticleLayer) {
                const count = layer.data.densityGrid[gridX]?.[gridY];
                if (count > 0) {
                    result.particles.push({ type: layer.data.type, count });
                }
            }
        }

        return result;
    }
}