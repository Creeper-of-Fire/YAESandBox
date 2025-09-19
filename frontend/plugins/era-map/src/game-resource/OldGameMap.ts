// export interface CellData {
//     objects: GameObjectRender[];
//     fields: { name: string; value: number }[];
//     particles: { type: string; count: number }[];
// }
//
// export class GameMap {
//     public readonly gridWidth: number;
//     public readonly gridHeight: number;
//     public readonly layers: (TileLayer | ObjectLayer | FieldLayer | ParticleLayer)[];
//
//     constructor(config: {
//         gridWidth: number;
//         gridHeight: number;
//         layers: (TileLayer | ObjectLayer | FieldLayer | ParticleLayer)[];
//     }) {
//         this.gridWidth = config.gridWidth;
//         this.gridHeight = config.gridHeight;
//         this.layers = config.layers;
//     }
//
//     /**
//      * 收集并返回指定网格坐标内的所有相关数据。
//      * @param gridX - 网格的X坐标
//      * @param gridY - 网格的Y坐标
//      * @returns 一个包含对象、场和粒子信息的对象
//      */
//     public getDataAtGridPosition(gridX: number, gridY: number): CellData {
//         const result: CellData = {
//             objects: [],
//             fields: [],
//             particles: [],
//         };
//
//         for (const layer of this.layers) {
//             if (layer instanceof ObjectLayer) {
//                 for (const obj of layer.objects) {
//                     const size = obj.config.gridSize || { width: 1, height: 1 };
//                     // 检查该格子是否在对象的占地范围内
//                     if (
//                         gridX >= obj.gridPosition.x && gridX < obj.gridPosition.x + size.width &&
//                         gridY >= obj.gridPosition.y && gridY < obj.gridPosition.y + size.height
//                     ) {
//                         result.objects.push(obj);
//                     }
//                 }
//             } else if (layer instanceof FieldLayer) {
//                 const value = layer.data[gridX]?.[gridY];
//                 if (value !== undefined) {
//                     result.fields.push({ name: layer.name, value });
//                 }
//             } else if (layer instanceof ParticleLayer) {
//                 const count = layer.data.densityGrid[gridX]?.[gridY];
//                 if (count > 0) {
//                     result.particles.push({ type: layer.data.type, count });
//                 }
//             }
//         }
//
//         return result;
//     }
// }