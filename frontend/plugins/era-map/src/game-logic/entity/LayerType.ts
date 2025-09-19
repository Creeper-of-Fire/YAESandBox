/**
 * 定义所有图层类型的唯一、稳定的标识符。
 * 这些值将作为数据契约的一部分，被序列化到存档中。
 * 它们不应该轻易改变，以保证向后兼容性。
 */
export const LayerType = {
    TileMapLayer: 'TileMapLayer',
    LogicalObjectLayer: 'LogicalObjectLayer',
    FieldContainerLayer: 'FieldContainerLayer',
    ParticleContainerLayer: 'ParticleContainerLayer',
} as const; // 'as const' 提供了更强的类型推断

// 创建一个联合类型，方便在其他地方进行类型约束
export type LayerTypeUnion = typeof LayerType[keyof typeof LayerType];