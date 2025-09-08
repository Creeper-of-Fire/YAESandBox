import type { RawGameObjectData } from './types';
import type { GameObject } from './GameObject';
import { createGameObject } from './GameObjectFactory';
import { Tileset, type TilesetConfig } from './Tileset';
import layoutJson from '#/assets/layout.json';

// --- 硬编码的布局数据 ---
const w = 40;
const T = 72;
const TAVERN_MAP_LAYOUT: number[][] = [
    [w, w, w, w, w, w, w, w, w, w],
    [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
    [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
    [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
    [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
    [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
    [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
    [w, w, w, w, w, w, w, w, w, w],
];

// 定义加载TavernMap所需的所有资产配置
interface TavernMapAssetConfig {
    layoutData: any; // 直接使用导入的JSON
    tileset: {
        url: string;
        sourceTileSize: number;
        columns: number;
    }
}

export class TavernMap {
    public readonly gridWidth: number;
    public readonly gridHeight: number;
    public readonly layout: number[][];
    public readonly gameObjects: GameObject[];
    public readonly tileset: Tileset;

    // 构造函数设为私有，强制使用异步工厂方法创建实例
    private constructor(
        layout: number[][],
        gameObjects: GameObject[],
        tileset: Tileset,
        meta: { gridWidth: number, gridHeight: number }
    ) {
        this.layout = layout;
        this.gameObjects = gameObjects;
        this.tileset = tileset;
        this.gridWidth = meta.gridWidth;
        this.gridHeight = meta.gridHeight;
    }

    /**
     * 异步工厂方法：加载所有必需的资产并创建一个完全初始化的TavernMap实例。
     */
    public static async create(assetConfig: TavernMapAssetConfig): Promise<TavernMap> {
        // 1. 加载图集图片
        const image = await TavernMap.loadImage(assetConfig.tileset.url);

        // 2. 创建Tileset实例
        const tileset = new Tileset({
            image,
            sourceTileSize: assetConfig.tileset.sourceTileSize,
            columns: assetConfig.tileset.columns,
        });

        // 3. 解析JSON数据，创建游戏对象
        const rawObjects: RawGameObjectData[] = assetConfig.layoutData.objects;
        const gameObjects = rawObjects
            .map(createGameObject)
            .filter((obj): obj is GameObject => obj !== null);

        // 4. 获取元数据
        const meta = assetConfig.layoutData.meta;

        // 5. 使用硬编码的布局
        const layout = TAVERN_MAP_LAYOUT;
        // 也可以在这里进行一些验证，比如布局的尺寸是否和meta中的尺寸匹配

        // 6. 所有数据准备就绪，创建并返回TavernMap实例
        return new TavernMap(layout, gameObjects, tileset, meta);
    }

    private static loadImage(url: string): Promise<HTMLImageElement> {
        return new Promise((resolve, reject) => {
            const image = new window.Image();
            image.src = url;
            image.onload = () => resolve(image);
            image.onerror = () => reject(new Error(`Failed to load image at ${url}`));
        });
    }
}