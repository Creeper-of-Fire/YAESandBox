import type {EntityInfo} from "#/game-logic/entity/entityInfo.ts";

export interface IGameEntity {
    /**
     * 实体的唯一标识符。
     * 对于 GameObjectEntity，这是 uuid。
     * 对于 FieldLayer，这可以是它的 name，例如 'light_level'，又或者我们之后也引入uuid，谁知道呢。
     */
    readonly id: string;

    /**
     * 实体的类型标识，用于在UI或逻辑中进行区分。
     * 例如 'GAME_OBJECT_ENTITY', 'FIELD_LAYER_ENTITY'
     */
    readonly entityType: string;

    /**
     * 当实体在某个特定网格坐标被查询时，它应该返回什么信息？
     * 这个方法是连接逻辑层和UI层的桥梁，但它只返回纯数据，不关心UI展现。
     * @param gridX - 查询点的 X 坐标
     * @param gridY - 查询点的 Y 坐标
     * @returns 如果该点在该实体范围内，则返回一个 EntityInfo 对象；否则返回 null。
     */
    getInfoAt(gridX: number, gridY: number): EntityInfo  | null;
}