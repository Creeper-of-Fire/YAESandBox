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
}