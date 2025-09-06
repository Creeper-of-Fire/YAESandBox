// 描述形状的DTO
export interface ShapeDTO {
    type: string;
    width?: number;
    height?: number;
    radius?: number;
}

// 描述一个物体如何产生一个场的DTO
export interface FieldGeneratorDTO {
    fieldName: string;      // 例如 'TABLE_REPULSION', 'CHAIR_ATTRACTION'
    // 关键：力的公式，使用模板字符串
    forceFormula: string;   // 例如 '1000 / (r * r)', '-50 / r'
    maxRange?: number;      // 场的作用范围，用于优化
}

// 描述一个物体如何响应一个场的DTO
export interface FieldResponderDTO {
    fieldName: string;      // 响应哪个场
    // 响应系数，可以是一个固定的数字，也可以是一个更复杂的表达式
    responseFactor: number | string; // 例如 0.5, '1 / mass'
}

// 最终的蓝图DTO
export interface GameObjectBlueprintDTO {
    type: string;           // 'TABLE', 'CHAIR'
    shape: ShapeDTO;
    physics: {
        density?: number;
        friction?: number;
    };
    collision: {
        groups: string[];
    };
    // 行为的核心：场的定义
    fieldGenerators?: FieldGeneratorDTO[];
    fieldResponders?: FieldResponderDTO[];
}