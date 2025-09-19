import { GameMap } from '#/game-logic/GameMap.ts';
import type { FullLayoutData } from '#/worldGeneration/types';
import { TileMapLayer } from '#/game-resource/TileMapLayer.ts';
import { FieldEntity } from '#/game-logic/entity/field/FieldEntity.ts';
import { FieldContainerLayer } from '#/game-logic/entity/field/render/FieldContainerLayer.ts';
import { ParticleEntity } from '#/game-logic/entity/particle/ParticleEntity.ts';
import { ParticleContainerLayer } from '#/game-logic/entity/particle/render/ParticleContainerLayer.ts';
import { createGameObjectEntity } from '#/game-logic/entity/gameObject/GameObjectEntityFactory.ts';
import type { GameObjectEntity } from '#/game-logic/entity/gameObject/GameObjectEntity.ts';
import { LogicalObjectLayer } from '#/game-logic/entity/gameObject/render/LogicalObjectLayer.ts';
import { kenney_roguelike_rpg_pack } from '#/game-resource/tilesetRegistry.ts';

/**
 * 一个临时的辅助函数，用于创建带墙壁的矩形背景。
 * TODO: 未来可以替换为更复杂的地图生成算法。
 */
function createWalledRectangle(width: number, height: number, wallTileId: number, floorTileId: number): number[][] {
    const layout: number[][] = [];
    for (let y = 0; y < height; y++) {
        const row: number[] = [];
        for (let x = 0; x < width; x++) {
            if (y === 0 || y === height - 1 || x === 0 || x === width - 1) {
                row.push(wallTileId);
            } else {
                row.push(floorTileId);
            }
        }
        layout.push(row);
    }
    return layout;
}

/**
 * 负责从原始布局数据生成一个全新的、功能完整的 GameMap 实例。
 * 这是一个纯粹的数据转换和组装服务。
 */
class WorldGenerationService {

    /**
     * 从 FullLayoutData 创建一个新的 GameMap 实例。
     * @param layoutData - 来自 init_layout.json 的原始数据。
     * @returns 一个全新的 GameMap 实例。
     */
    public createFromInitialLayout(layoutData: FullLayoutData): GameMap {
        const layers = [];

        // 1. 【硬编码逻辑】创建背景瓦片层
        const wallTile = kenney_roguelike_rpg_pack.tileItem.stone_wall;
        const floorTile = kenney_roguelike_rpg_pack.tileItem.wooden_floor;
        const backgroundLayout = createWalledRectangle(layoutData.meta.gridWidth, layoutData.meta.gridHeight, wallTile, floorTile);
        layers.push(new TileMapLayer({
            tilesetId: kenney_roguelike_rpg_pack.id,
            data: backgroundLayout,
        }));

        // 2. 根据数据创建场实体和图层
        const fieldEntities = Object.entries(layoutData.fields).map(([name, data]) => new FieldEntity({ name, data }));
        if (fieldEntities.length > 0) {
            layers.push(new FieldContainerLayer(fieldEntities));
        }

        // 3. 根据数据创建粒子实体和图层
        const particleEntities = Object.values(layoutData.particles).map(particleData => new ParticleEntity(particleData));
        if (particleEntities.length > 0) {
            layers.push(new ParticleContainerLayer(particleEntities));
        }

        // 4. 根据数据创建游戏对象实体和图层
        const logicalObjects = layoutData.objects
            .map(createGameObjectEntity)
            .filter((o): o is GameObjectEntity => o !== null);
        layers.push(new LogicalObjectLayer(logicalObjects));

        // 5. 组装并返回最终的 GameMap 实例
        return new GameMap({
            gridWidth: layoutData.meta.gridWidth,
            gridHeight: layoutData.meta.gridHeight,
            layers: layers,
        });
    }
}

export const worldGenerationService = new WorldGenerationService();