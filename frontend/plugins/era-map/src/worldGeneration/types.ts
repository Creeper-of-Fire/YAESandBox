// 代表来自Python脚本的原始JSON对象结构
export interface RawGameObjectData {
    obj_type: string;
    grid_pos: number[];
    grid_size?: number[];
    visual_pos: number[];
    visual_angle: number;
}


export interface FieldLayerData {
    // 场数据是数值类型
    [fieldName: string]: number[][];
}

export interface ParticleConfig {
    colorRange: [string, string];
    sizeRange: [number, number];
    placement: string;
    // ... 未来可能还有其他配置
}

export interface ParticleLayerData {
    type: string;
    seed: number;
    densityGrid: number[][]; // 一般来说应该是整数
}

export interface AllParticleLayersData {
    [particleName: string]: ParticleLayerData;
}

export interface LayoutMeta {
    gridWidth: number;
    gridHeight: number;
}

export interface FullLayoutData {
    meta: LayoutMeta;
    objects: RawGameObjectData[];
    fields: FieldLayerData;
    particles: AllParticleLayersData;
}
