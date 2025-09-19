import {GameObjectRender} from '#/game-logic/entity/gameObject/render/GameObjectRender.ts';
import {Expose, Type} from 'class-transformer';
import type {IGameEntity} from "#/game-logic/entity/IGameEntity.ts";
import {EntityInfoType, type GameObjectInfo} from "#/game-logic/entity/entityInfo.ts";

/**
 * 代表游戏世界中的一个逻辑实体。
 * 这是 "世界状态" 的核心组成部分，包含了渲染信息和由AI生成的、丰富的逻辑属性。
 */
export class GameObjectEntity implements IGameEntity
{

    @Expose()
    public readonly entityType: string = 'GAME_OBJECT_ENTITY' as const;

    /**
     * 对象的唯一标识符，是整个系统中的主键。
     */
    @Expose()
    public readonly id: string;

    /**
     * 对象的类型，例如 'TABLE', 'ELF'。
     */
    @Expose()
    public readonly type: string;

    /**
     * 包含了所有用于渲染所需的数据（位置、旋转、尺寸、图块等）。
     * 它是一个独立的、职责单一的渲染数据载体。
     */
    @Expose()
    @Type(() => GameObjectRender)
    public readonly renderInfo: GameObjectRender;

    /**
     * "AI记事本"：一个灵活的字典，用于存储所有非渲染的逻辑数据。
     * AI工作流生成的所有丰富化细节（如描述、历史、隐藏属性等）都存放在这里。
     * 其结构是动态的，可以适应不同AI提案返回的数据。
     */
    @Expose()
    public properties: Record<string, any>;

    constructor(id: string, type: string, renderInfo: GameObjectRender)
    {
        this.id = id;
        this.type = type;
        this.renderInfo = renderInfo;
        this.properties = {}; // 初始为空，等待AI填充
    }

    public getInfoAt(gridX: number, gridY: number): GameObjectInfo | null
    {
        const box = this.getGridBoundingBox();
        if (
            gridX >= box.x && gridX < box.x + box.width &&
            gridY >= box.y && gridY < box.y + box.height
        )
        {
            return {
                type: EntityInfoType.GameObject,
                entity: this,
            };
        }
        return null;
    }

    private getGridBoundingBox()
    {
        const size = this.renderInfo.config.gridSize || {width: 1, height: 1};
        return {
            x: this.renderInfo.gridPosition.x,
            y: this.renderInfo.gridPosition.y,
            width: size.width,
            height: size.height,
        };
    }
}