import type Matter from 'matter-js';


/**
 * Shape 接口定义了所有几何形状的通用契约。
 * 核心职责是，每个形状必须知道如何为自己创建一个对应的
 * Matter.js 物理实体（Body）。
 */
export interface Shape {
    /**
     * 一个只读的字符串，用于标识形状的类型。
     * 主要用于调试和可能的序列化。
     */
    readonly type: string;

    /**
     * 每个 Shape 实现都必须知道如何为自己创建一个
     * 对应的 Matter.js 物理实体。
     * @param x - 初始 x 坐标
     * @param y - 初始 y 坐标
     * @param options - 通用的物理和碰撞选项
     * @returns 一个创建好的 Matter.Body 实例
     */
    createBody(x: number, y: number, options: Matter.IBodyDefinition): Matter.Body;

    /**
     * 为该形状提供渲染所需的 Konva 配置。
     * @param body - 对应的 Matter.Body 实例，用于获取动态数据（位置、角度）。
     * @returns 一个包含 Konva 组件类型和其 props 的对象。
     */
    getRenderConfig(body: Matter.Body): { component: string; config: Record<string, any> };
}

/**
 * 代表一个二维向量或点。
 * 这是我们领域模型中的基本几何构件。
 */
export interface Vertex {
    readonly x: number;
    readonly y: number;
}
