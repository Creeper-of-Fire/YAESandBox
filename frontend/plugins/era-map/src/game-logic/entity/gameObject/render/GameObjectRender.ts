import type {GameObjectConfig} from '#/game-resource/types.ts';
import type {RawGameObjectData} from '#/worldGeneration/types.ts'
import {TILE_SIZE} from '#/constant.ts';
import {Expose} from 'class-transformer';

export class GameObjectRender
{
    @Expose() public readonly id: string;
    @Expose() public readonly type: string;
    @Expose() public readonly config: GameObjectConfig;

    // 逻辑位置（网格坐标）
    @Expose()
    public readonly gridPosition: { x: number, y: number };
    // 渲染位置（像素坐标），永远代表对象的【中心点】
    @Expose()
    public position: { x: number, y: number };
    @Expose()
    public rotation: number;
    // 尺寸信息（像素单位）
    @Expose()
    public size: { width: number, height: number };

    constructor(id: string, data: RawGameObjectData, config: GameObjectConfig)
    {
        // class-transformer必定会调用这个构造函数，然后传入一大堆undefined，我们在这里糊弄它和ts
        // class-transformer传入的undefined本质上是无害的，因为它会立刻在调用构造函数后开始填充，但是如果在构造函数里面调用undefined对象的属性，就会抛出错误
        if (!id || !data || !config)
        {
            this.id = '';
            this.type = 'UNKNOWN';
            this.config = {} as any;
            this.gridPosition = { x: 0, y: 0 };
            this.position = { x: 0, y: 0 };
            this.rotation = 0;
            this.size = { width: 0, height: 0 };
            return
        }

        if (data.grid_pos.length !== 2 || data.visual_pos.length !== 2)
        {
            // 抛出一个明确的错误，而不是静默失败
            throw new Error(`Invalid position data for object type "${data.obj_type}". Expected 2-element arrays.`);
        }
        if (data.grid_size && data.grid_size.length !== 2)
        {
            throw new Error(`Invalid grid_size for object type "${data.obj_type}". Expected a 2-element array.`);
        }

        this.id = id;
        this.type = data.obj_type;
        this.config = config;
        this.rotation = data.visual_angle;

        // --- 将两种不同的`visual_pos`统一为渲染中心点`this.position` ---
        // 这是本类的核心职责之一：作为“反腐败层”，将源数据的不一致性转换为内部的一致性。

        if (config.gridSize)
        {
            // **情况1: 网格对齐的对象 (如桌子、椅子)**
            // 源数据 `visual_pos` 代表对象的【左上角】坐标。
            // 尺寸由配置决定。
            this.size = {
                width: config.gridSize.width * TILE_SIZE,
                height: config.gridSize.height * TILE_SIZE
            };

            const topLeftX = data.visual_pos[0] * TILE_SIZE;
            const topLeftY = data.visual_pos[1] * TILE_SIZE;

            // 计算中心点
            this.position = {
                x: topLeftX + this.size.width / 2,
                y: topLeftY + this.size.height / 2
            };
        }
        else
        {
            // **情况2: 自由浮动的对象 (如角色、脏污)**
            // 源数据 `visual_pos` 代表对象的【中心点】坐标。
            // 尺寸通常是默认的 1x1 瓦片大小。
            this.size = {width: TILE_SIZE, height: TILE_SIZE};

            // 直接使用源数据作为中心点
            this.position = {
                x: data.visual_pos[0] * TILE_SIZE,
                y: data.visual_pos[1] * TILE_SIZE
            };
        }

        // --- 存储逻辑网格位置 ---
        this.gridPosition = {x: data.grid_pos[0], y: data.grid_pos[1]};
    }
}