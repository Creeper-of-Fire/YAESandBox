// 从 GameObject 中提取的、用于在UI中展示的最小信息集
export interface SelectedObjectInfo {
    id: string;
    type: string;
    // 未来可以添加更多描述性信息，如名称、状态等
}

// 用于展示的场信息
export interface SelectedFieldInfo {
    name: string; // e.g., 'light_level'
    value: number;
}

// 用于展示的粒子信息
export interface SelectedParticleInfo {
    type: string;
    count: number;
}

// Pinia store中存储的完整选择信息
export interface SelectionDetails {
    objects: SelectedObjectInfo[];
    fields: SelectedFieldInfo[];
    particles: SelectedParticleInfo[];
}