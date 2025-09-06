import { GameObject } from './GameObject';
import { type Shape } from './shapes/Shape';

// 代表一个已编译的力函数
export type ForceFunction = (distance: number) => number;

// 代表一个已编译的场生成器
export interface CompiledFieldGenerator {
    fieldName: string;
    calculateForce: ForceFunction;
    maxRangeSq: number; // 使用距离的平方进行比较以提高性能
}

// 代表一个已编译的场响应器
export interface CompiledFieldResponder {
    fieldName: string;
    responseFactor: number; // 简单起见，我们先假设它是数字
}

export class ConfigurableGameObject extends GameObject {

    public readonly collisionGroups: string[]; // 只声明自己属于哪些组

    // 编译后的行为
    public readonly fieldGenerators: CompiledFieldGenerator[];
    public readonly fieldResponders: CompiledFieldResponder[];

    constructor(
        id: string,
        name: string,
        shape: Shape,
        collisionGroup: string[],
        fieldGenerators: CompiledFieldGenerator[],
        fieldResponders: CompiledFieldResponder[]
    ) {
        super(id, name, shape);
        this.collisionGroups = collisionGroup;
        this.fieldGenerators = fieldGenerators;
        this.fieldResponders = fieldResponders;
    }
}