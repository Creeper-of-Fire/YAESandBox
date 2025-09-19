// 实体类型枚举，用于可辨识联合
export enum EntityInfoType {
    GameObject = 'GAME_OBJECT',
    Field = 'FIELD',
    Particle = 'PARTICLE',
}

// 各种实体在被查询时返回的具体信息结构
export interface GameObjectInfo {
    type: EntityInfoType.GameObject;
    entity: import('./gameObject/GameObjectEntity').GameObjectEntity; // 直接传递整个实体引用
}

export interface FieldInfo {
    type: EntityInfoType.Field;
    name: string; // e.g., 'light_level'
    value: number;
}

export interface ParticleInfo {
    type: EntityInfoType.Particle;
    particleType: string; // e.g., 'dust_motes'
    density: number; // 该格子的粒子密度
}

// 最终的联合类型
export type EntityInfo = GameObjectInfo | FieldInfo | ParticleInfo;