import { type Shape } from './shapes/Shape';

// 所有游戏对象的抽象基类
export abstract class GameObject {
    public readonly id: string;
    public readonly name: string;
    public readonly shape: Shape;

    // 碰撞组的定义，使用字符串名称
    public abstract readonly collisionGroups: string[];
    protected constructor(id: string, name:string, shape: Shape) {
        this.id = id;
        this.name = name;
        this.shape = shape;
    }
}