// 代表来自Python脚本的原始JSON对象结构
export interface RawGameObjectData {
    obj_type: string;
    grid_pos: number[];
    grid_size?: number[];
    visual_pos: number[];
    visual_angle: number;
}

// 渲染类型的枚举，决定了使用哪个渲染器
export enum RenderType {
    SHAPE = 'SHAPE', // 绘制基本图形 (矩形, 圆形)
    SPRITE = 'SPRITE', // 从图集中绘制图片
}

// 基础配置接口
interface BaseConfig {
    renderType: RenderType;
}

// 形状渲染器的配置
export interface ShapeRenderConfig extends BaseConfig {
    renderType: RenderType.SHAPE;
    shape: 'rect' | 'circle';
    fill: string;
    stroke?: string;
    size?: { width: number, height: number }; // 用于非网格绑定的形状，如角色
    radius?: number; // 用于圆形
}

export interface SpriteComponent {
    tileId: number;
    // 相对于物体原点(0,0)的偏移量，单位为网格
    offset: { x: number; y: number };
}

// 图片渲染器的配置
export interface SpriteRenderConfig extends BaseConfig {
    renderType: RenderType.SPRITE;
    // 一个物体由一个或多个瓦片组件构成
    components: SpriteComponent[];
}

// 联合类型，涵盖所有可能的渲染配置
export type RenderConfig = ShapeRenderConfig | SpriteRenderConfig;

// 游戏对象的完整配置
export interface GameObjectConfig {
    // 如果是基于网格的物体，这里定义其大小
    gridSize?: { width: number, height: number };
    renderConfig: RenderConfig;
}