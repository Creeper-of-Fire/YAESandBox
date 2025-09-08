export interface TilesetConfig {
    image: HTMLImageElement;
    sourceTileSize: number; // 源文件中的瓦片尺寸
    columns: number;
}

export class Tileset {
    public readonly image: HTMLImageElement;
    public readonly sourceTileSize: number;
    public readonly columns: number;

    constructor(config: TilesetConfig) {
        this.image = config.image;
        this.sourceTileSize = config.sourceTileSize;
        this.columns = config.columns;
    }

    /**
     * 根据瓦片ID计算其在图集中的裁剪信息
     * @param tileId - 瓦片的ID
     * @returns Konva.Image的crop配置对象
     */
    public getTileCrop(tileId: number) {
        if (tileId < 0) {
            // TODO 可以返回一个透明或者错误的瓦片，或者直接返回null/undefined让上层处理
            return { x: 0, y: 0, width: 0, height: 0 };
        }
        const x = (tileId % this.columns) * this.sourceTileSize;
        const y = Math.floor(tileId / this.columns) * this.sourceTileSize;
        return {
            x: x,
            y: y,
            width: this.sourceTileSize,
            height: this.sourceTileSize,
        };
    }
}