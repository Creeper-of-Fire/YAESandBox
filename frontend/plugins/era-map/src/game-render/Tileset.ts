export interface TilesetConfig {
    image: HTMLImageElement;
    sourceTileSize: number; // 源文件中的瓦片尺寸
    columns: number;
    spacing?: number; // 瓦片之间的间隙，默认为0
    margin?: number;  // 整张图集边缘的留白，默认为0
}

export class Tileset {
    public readonly image: HTMLImageElement;
    public readonly sourceTileSize: number;
    public readonly columns: number;
    public readonly spacing: number;
    public readonly margin: number;

    constructor(config: TilesetConfig) {
        this.image = config.image;
        this.sourceTileSize = config.sourceTileSize;
        this.columns = config.columns;
        this.spacing = config.spacing || 0;
        this.margin = config.margin || 0;
    }

    /**
     * 根据瓦片ID计算其在图集中的裁剪信息
     * @param tileId - 瓦片的ID
     * @returns Konva.Image的crop配置对象
     */
    public getTileCrop(tileId: number) {
        if (tileId < 0) {
            return { x: 0, y: 0, width: 0, height: 0 };
        }

        // 计算瓦片在网格中的逻辑坐标 (第几行第几列)
        const tileX = tileId % this.columns;
        const tileY = Math.floor(tileId / this.columns);

        // 计算裁剪的实际像素坐标
        const cropX = this.margin + tileX * (this.sourceTileSize + this.spacing);
        const cropY = this.margin + tileY * (this.sourceTileSize + this.spacing);

        return {
            x: cropX,
            y: cropY,
            width: this.sourceTileSize,
            height: this.sourceTileSize,
        };
    }
}