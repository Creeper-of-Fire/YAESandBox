export enum InteractableType {
    GameObject = 'GAME_OBJECT',
    Field = 'FIELD',
    Particle = 'PARTICLE'
}

// 描述一个可交互实体的核心信息
export interface IInteractable {
    readonly id: string; // 唯一ID
    readonly type: InteractableType; // 实体的大类
    readonly name: string; // 用于显示的名称, e.g., 'TABLE', 'light_level'

    /**
     * 获取用于在UI（如SelectionPanel）中显示的详细信息
     * @returns 一个键值对对象
     */
    getSelectionDetails(): Record<string, any>;
}