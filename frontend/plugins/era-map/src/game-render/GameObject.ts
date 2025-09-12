import type { RawGameObjectData, GameObjectConfig } from './types';
import { TILE_SIZE } from '../constant';
import { v4 as uuidv4 } from 'uuid';

export class GameObject {
    public readonly id: string;
    public readonly type: string;
    public readonly config: GameObjectConfig;

    // 逻辑位置（网格坐标）
    public readonly gridPosition: { x: number, y: number };
    // 渲染位置（像素坐标），永远代表对象的【中心点】
    public position: { x: number, y: number };
    public rotation: number;
    // 尺寸信息（像素单位）
    public size: { width: number, height: number };

    constructor(data: RawGameObjectData, config: GameObjectConfig) {
        if (data.grid_pos.length !== 2 || data.visual_pos.length !== 2) {
            // 抛出一个明确的错误，而不是静默失败
            throw new Error(`Invalid position data for object type "${data.obj_type}". Expected 2-element arrays.`);
        }
        if (data.grid_size && data.grid_size.length !== 2) {
            throw new Error(`Invalid grid_size for object type "${data.obj_type}". Expected a 2-element array.`);
        }

        this.id = uuidv4(); // 为每个对象生成唯一ID，便于Vue的key绑定
        this.type = data.obj_type;
        this.config = config;
        this.rotation = data.visual_angle;

        // --- 将两种不同的`visual_pos`统一为渲染中心点`this.position` ---
        // 这是本类的核心职责之一：作为“反腐败层”，将源数据的不一致性转换为内部的一致性。

        if (config.gridSize) {
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
        } else {
            // **情况2: 自由浮动的对象 (如角色、脏污)**
            // 源数据 `visual_pos` 代表对象的【中心点】坐标。
            // 尺寸通常是默认的 1x1 瓦片大小。
            this.size = { width: TILE_SIZE, height: TILE_SIZE };

            // 直接使用源数据作为中心点
            this.position = {
                x: data.visual_pos[0] * TILE_SIZE,
                y: data.visual_pos[1] * TILE_SIZE
            };
        }

        // --- 存储逻辑网格位置 ---
        this.gridPosition = { x: data.grid_pos[0], y: data.grid_pos[1] };
    }
}