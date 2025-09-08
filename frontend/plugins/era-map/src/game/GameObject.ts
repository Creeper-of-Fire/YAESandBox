import type { RawGameObjectData, GameObjectConfig } from './types';
import { TILE_SIZE } from '../constant';
import { v4 as uuidv4 } from 'uuid';

export class GameObject {
    public readonly id: string;
    public readonly type: string;
    public readonly config: GameObjectConfig;

    // 逻辑位置（网格坐标）
    public readonly gridPosition: { x: number, y: number };
    // 渲染位置（像素坐标）
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

        // --- 首先计算尺寸 ---
        if (config.gridSize) {
            // 对于绑定到网格的物体，尺寸由配置决定
            this.size = {
                width: config.gridSize.width * TILE_SIZE,
                height: config.gridSize.height * TILE_SIZE
            };
        } else {
            // 对于独立物体，尺寸可能需要从渲染配置中推断
            // 这里为了简单，先给个默认值
            this.size = { width: TILE_SIZE, height: TILE_SIZE };
        }

        // --- 存储两种位置 ---
        // 1. 存储逻辑网格位置 (这个不变)
        this.gridPosition = { x: data.grid_pos[0], y: data.grid_pos[1] };

        // 2. 计算用于渲染的中心点精确像素位置
        const topLeftX = data.visual_pos[0] * TILE_SIZE;
        const topLeftY = data.visual_pos[1] * TILE_SIZE;

        this.position = {
            x: topLeftX + this.size.width / 2,
            y: topLeftY + this.size.height / 2
        };
    }
}