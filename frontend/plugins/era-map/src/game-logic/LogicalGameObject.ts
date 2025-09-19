import type {GameObjectRender} from '#/game-render/GameObjectRender';

/**
 * 代表游戏世界中的一个逻辑实体。
 * 这是 "世界状态" 的核心组成部分，包含了渲染信息和由AI生成的、丰富的逻辑属性。
 */
export class LogicalGameObject
{

    public readonly entityType: string = 'LOGICAL_GAME_OBJECT' as const;

    /**
     * 对象的唯一标识符，是整个系统中的主键。
     */
    public readonly id: string;

    /**
     * 对象的类型，例如 'TABLE', 'ELF'。
     */
    public readonly type: string;

    /**
     * 包含了所有用于渲染所需的数据（位置、旋转、尺寸、图块等）。
     * 它是一个独立的、职责单一的渲染数据载体。
     */
    public readonly renderInfo: GameObjectRender;

    /**
     * "AI记事本"：一个灵活的字典，用于存储所有非渲染的逻辑数据。
     * AI工作流生成的所有丰富化细节（如描述、历史、隐藏属性等）都存放在这里。
     * 其结构是动态的，可以适应不同AI提案返回的数据。
     */
    public properties: Record<string, any>;

    constructor(id: string, type: string, renderInfo: GameObjectRender)
    {
        this.id = id;
        this.type = type;
        this.renderInfo = renderInfo;
        this.properties = {}; // 初始为空，等待AI填充
    }

    toJSON()
    {
        return {
            id: this.id,
            type: this.type,
            renderInfo: this.renderInfo, // renderInfo本身已经是POJO-like
            properties: this.properties,
        };
    }
}