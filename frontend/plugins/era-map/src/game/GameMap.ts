import type { GameObject } from './GameObject';
import { defineAsyncComponent, type Component } from 'vue';


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

export class GameMap {
    public readonly gridWidth: number;
    public readonly gridHeight: number;
    public readonly layers: (TileLayer | ObjectLayer)[];

    constructor(config: {
        gridWidth: number;
        gridHeight: number;
        layers: (TileLayer | ObjectLayer)[];
    }) {
        this.gridWidth = config.gridWidth;
        this.gridHeight = config.gridHeight;
        this.layers = config.layers;
    }
}