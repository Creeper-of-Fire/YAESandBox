import {Tileset} from './Tileset';

export type TileSetConfig = {
    id: string;
    url: string;
    sourceTileSize: number;
    columns: number;
    spacing?: number;
    margin?: number;
}

class ResourceManager
{
    private tilesets = new Map<string, Tileset>();
    private imageCache = new Map<string, HTMLImageElement>();

    public async loadTileset(config: TileSetConfig): Promise<Tileset>
    {
        if (this.tilesets.has(config.id))
        {
            console.warn(`Tileset with id "${config.id}" already loaded.`);
            return this.tilesets.get(config.id)!;
        }

        const image = await this.loadImage(config.url);
        const tileset = new Tileset({
            image,
            sourceTileSize: config.sourceTileSize,
            columns: config.columns,
            spacing: config.spacing,
            margin: config.margin,
        });
        this.tilesets.set(config.id, tileset);
        return tileset;
    }

    public getTileset(id: string): Tileset | undefined
    {
        const tileset = this.tilesets.get(id);
        if (!tileset)
        {
            throw new Error(`Tileset with id "${id}" not found. Was it loaded?`);
        }
        return tileset;
    }

    private async loadImage(url: string): Promise<HTMLImageElement>
    {
        if (this.imageCache.has(url))
        {
            return this.imageCache.get(url)!;
        }
        return new Promise((resolve, reject) =>
        {
            const image = new window.Image();
            image.src = url;
            image.onload = () =>
            {
                this.imageCache.set(url, image);
                resolve(image);
            };
            image.onerror = () => reject(new Error(`Failed to load image at ${url}`));
        });
    }
}

// 导出一个单例，方便全局访问
export const resourceManager = new ResourceManager();