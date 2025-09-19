import { defineStore } from 'pinia';
import { computed, type Ref, ref } from 'vue';
import { GameMap } from '#/game-logic/GameMap.ts';
import { createGameObjectEntity } from '#/game-logic/entity/gameObject/GameObjectEntityFactory.ts';
import type { GameObjectEntity } from '#/game-logic/entity/gameObject/GameObjectEntity.ts';
import type { FullLayoutData } from '#/game-resource/types';
import { kenney_roguelike_rpg_pack } from '#/game-resource/tilesetRegistry';
import { TileMapLayer } from "#/game-resource/TileMapLayer.ts";
import { LogicalObjectLayer } from "#/game-logic/entity/gameObject/render/LogicalObjectLayer.ts";
import { FieldEntity } from "#/game-logic/entity/field/FieldEntity.ts";
import { FieldContainerLayer } from "#/game-logic/entity/field/render/FieldContainerLayer.ts";
import { ParticleEntity } from "#/game-logic/entity/particle/ParticleEntity.ts";
import { ParticleContainerLayer } from "#/game-logic/entity/particle/render/ParticleContainerLayer.ts";

// 一个辅助函数，暂时放在这里
function createWalledRectangle(width: number, height: number, wallTileId: number, floorTileId: number): number[][]
{
    const layout: number[][] = [];
    for (let y = 0; y < height; y++)
    {
        const row: number[] = [];
        for (let x = 0; x < width; x++)
        {
            if (y === 0 || y === height - 1 || x === 0 || x === width - 1)
            {
                row.push(wallTileId);
            }
            else
            {
                row.push(floorTileId);
            }
        }
        layout.push(row);
    }
    return layout;
}

export const useWorldStateStore = defineStore('world-state', () =>
{
    // --- State ---
    const logicalGameMap: Ref<GameMap | null> = ref(null);
    const isLoaded = ref(false);
    const error = ref<string | null>(null);

    // --- Getter ---
    const allObjects = computed(() =>
    {
        if (!logicalGameMap.value) return [];
        const objectLayer = logicalGameMap.value.layers.find(l => l instanceof LogicalObjectLayer);
        return objectLayer ? (objectLayer as LogicalObjectLayer).objects : [];
    });

    // --- Actions ---

    /**
     * 从原始布局数据加载并初始化整个世界状态。
     * 这是会话开始时调用的第一个关键操作。
     */
    function loadInitialState(layoutData: FullLayoutData)
    {
        try
        {
            const layers = [];

            // 1. 创建渲染层 (瓦片、场、粒子)
            const w = kenney_roguelike_rpg_pack.tileItem.stone_wall;
            const f = kenney_roguelike_rpg_pack.tileItem.wooden_floor;
            // TODO 之后改成更好的背景图生成
            const backgroundLayout = createWalledRectangle(layoutData.meta.gridWidth, layoutData.meta.gridHeight, w, f);
            layers.push(new TileMapLayer({
                tilesetId: kenney_roguelike_rpg_pack.id,
                data: backgroundLayout,
            }));

            const fieldEntities = Object.entries(layoutData.fields).map(([name, data]) => {
                return new FieldEntity({ name, data });
            });
            if (fieldEntities.length > 0) {
                layers.push(new FieldContainerLayer(fieldEntities));
            }

            const particleEntities = Object.values(layoutData.particles).map(particleData => {
                return new ParticleEntity(particleData);
            });
            if (particleEntities.length > 0) {
                layers.push(new ParticleContainerLayer(particleEntities));
            }

            // 2. 创建逻辑对象层
            const logicalObjects = layoutData.objects
                .map(createGameObjectEntity)
                .filter((o): o is GameObjectEntity => o !== null);
            layers.push(new LogicalObjectLayer(logicalObjects));

            // 3. 创建并存储 LogicalGameMap 实例
            logicalGameMap.value = new GameMap({
                gridWidth: layoutData.meta.gridWidth,
                gridHeight: layoutData.meta.gridHeight,
                layers: layers,
            });

            isLoaded.value = true;
            error.value = null;
            console.log("World state initialized successfully.", logicalGameMap.value);

        } catch (e)
        {
            console.error("Failed to load initial state:", e);
            error.value = (e as Error).message;
            isLoaded.value = false;
        }
    }

    /**
     * 将AI的提案应用到指定对象上。
     * 这是唯一允许修改世界状态中对象属性的入口点，保证了数据流的单向性。
     * @param proposal - 包含目标对象ID和要合并的属性数据。
     */
    function applyProposal(proposal: { targetObjectId: string; data: Record<string, any> })
    {
        if (!logicalGameMap.value)
        {
            console.error("Cannot apply proposal: World state is not loaded.");
            return;
        }

        const targetObject = logicalGameMap.value.findObjectById(proposal.targetObjectId);

        if (targetObject)
        {
            // 使用对象展开语法进行浅合并，这对于原型阶段足够了
            // TODO 对于深层嵌套的属性，未来可能需要一个深合并工具函数
            targetObject.properties = {
                ...targetObject.properties,
                ...proposal.data,
            };
            console.log(`Proposal applied to object ${proposal.targetObjectId}`, targetObject);
        }
        else
        {
            console.error(`Cannot apply proposal: Object with ID ${proposal.targetObjectId} not found.`);
        }
    }

    /**
     * (未来功能) 序列化当前世界状态为可导出的JSON。
     */
    function exportState()
    {
        // TODO: 实现将 logicalGameMap 转换为 final_game_state.json 格式的逻辑
        console.warn("exportState() is not yet implemented.");
    }

    return {
        // State
        logicalGameMap,
        isLoaded,
        error,
        // Getter
        allObjects,
        // Actions
        loadInitialState,
        applyProposal,
        exportState,
    };
});