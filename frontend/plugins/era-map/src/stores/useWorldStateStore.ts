import { defineStore } from 'pinia';
import { ref, type Ref } from 'vue';
import { LogicalGameMap, LogicalObjectLayer } from '#/game-logic/LogicalGameMap';
import { TileLayer, FieldLayer, ParticleLayer } from '#/game-render/GameMap';
import { createLogicalGameObject } from '#/game-logic/LogicalGameObjectFactory';
import type { LogicalGameObject } from '#/game-logic/LogicalGameObject';
import type { FullLayoutData } from '#/game-render/types';
import { kenney_roguelike_rpg_pack } from '#/game-render/tilesetRegistry';

// 一个辅助函数，暂时放在这里
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

export const useWorldStateStore = defineStore('world-state', () => {
    // --- State ---
    const logicalGameMap: Ref<LogicalGameMap | null> = ref(null);
    const isLoaded = ref(false);
    const error = ref<string | null>(null);

    // --- Actions ---

    /**
     * 从原始布局数据加载并初始化整个世界状态。
     * 这是会话开始时调用的第一个关键操作。
     */
    function loadInitialState(layoutData: FullLayoutData) {
        try {
            const layers = [];

            // 1. 创建渲染层 (瓦片、场、粒子) - 这部分逻辑和旧 TavernMap 类似
            const w = kenney_roguelike_rpg_pack.tileItem.stone_wall;
            const f = kenney_roguelike_rpg_pack.tileItem.wooden_floor;
            const backgroundLayout = createWalledRectangle(layoutData.meta.gridWidth, layoutData.meta.gridHeight, w, f);
            layers.push(new TileLayer({
                tilesetId: kenney_roguelike_rpg_pack.id,
                data: backgroundLayout,
            }));

            for (const fieldName in layoutData.fields) {
                layers.push(new FieldLayer({ name: fieldName, data: layoutData.fields[fieldName] }));
            }

            for (const particleName in layoutData.particles) {
                layers.push(new ParticleLayer(layoutData.particles[particleName]));
            }

            // 2. 创建逻辑对象层
            const logicalObjects = layoutData.objects
                .map(createLogicalGameObject)
                .filter((o): o is LogicalGameObject => o !== null);
            layers.push(new LogicalObjectLayer(logicalObjects));

            // 3. 创建并存储 LogicalGameMap 实例
            logicalGameMap.value = new LogicalGameMap({
                gridWidth: layoutData.meta.gridWidth,
                gridHeight: layoutData.meta.gridHeight,
                layers: layers,
            });

            isLoaded.value = true;
            error.value = null;
            console.log("World state initialized successfully.", logicalGameMap.value);

        } catch (e) {
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
    function applyProposal(proposal: { targetObjectId: string; data: Record<string, any> }) {
        if (!logicalGameMap.value) {
            console.error("Cannot apply proposal: World state is not loaded.");
            return;
        }

        const targetObject = logicalGameMap.value.findObjectById(proposal.targetObjectId);

        if (targetObject) {
            // 使用对象展开语法进行浅合并，这对于原型阶段足够了
            // TODO 对于深层嵌套的属性，未来可能需要一个深合并工具函数
            targetObject.properties = {
                ...targetObject.properties,
                ...proposal.data,
            };
            console.log(`Proposal applied to object ${proposal.targetObjectId}`, targetObject);
        } else {
            console.error(`Cannot apply proposal: Object with ID ${proposal.targetObjectId} not found.`);
        }
    }

    /**
     * (未来功能) 序列化当前世界状态为可导出的JSON。
     */
    function exportState() {
        // TODO: 实现将 logicalGameMap 转换为 final_game_state.json 格式的逻辑
        console.warn("exportState() is not yet implemented.");
    }

    return {
        // State
        logicalGameMap,
        isLoaded,
        error,
        // Actions
        loadInitialState,
        applyProposal,
        exportState,
    };
});