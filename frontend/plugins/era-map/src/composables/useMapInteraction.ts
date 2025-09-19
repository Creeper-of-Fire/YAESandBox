// src/composables/useMapInteraction.ts

import type {Ref} from 'vue';
import {computed, ref} from 'vue';
import {TILE_SIZE} from '#/constant';
import {useSelectionStore} from '#/game-logic/selectionStore';
import type {GameMap} from "#/game-logic/GameMap.ts";
import type {IGameEntity} from "#/game-logic/entity/entity.ts";

export function useMapInteraction(gameMap: Ref<GameMap | null>)
{
    const selectionStore = useSelectionStore();

    // --- State: Hover (鼠标悬浮) ---
    const hoveredGridPos = ref({x: -1, y: -1});
    const hoveredEntities = ref<IGameEntity[]>([]);
    const popoverTargetPosition = ref({x: 0, y: 0});
    const showPopover = ref(false);

    // --- State: Selection (鼠标点击选中) ---
    const selectedGridPos = ref({x: -1, y: -1});


    // === Computed Properties ===

    // 1. Hover 相关
    const hasHoveredData = computed(() => hoveredEntities.value.length > 0);

    // 2. Selection 相关
    const isACellSelected = computed(() => selectedGridPos.value.x !== -1 && selectedGridPos.value.y !== -1);

    // 3. Hover 与 Selection 交互
    /** 计算当前悬浮的格子是否就是已被选中的格子 */
    const isHoveringSelectedCell = computed(() =>
    {
        return isACellSelected.value &&
            hoveredGridPos.value.x === selectedGridPos.value.x &&
            hoveredGridPos.value.y === selectedGridPos.value.y;
    });

    // === Konva Configs ===

    /** 悬浮高亮框 (黄色) */
    const highlightBoxConfig = computed(() => ({
        x: hoveredGridPos.value.x * TILE_SIZE,
        y: hoveredGridPos.value.y * TILE_SIZE,
        width: TILE_SIZE,
        height: TILE_SIZE,
        stroke: '#FFFFAA', // 亮黄色
        strokeWidth: 2,
        listening: false,
    }));

    /** 选中状态框 (蓝色) */
    const selectionBoxConfig = computed(() => ({
        x: selectedGridPos.value.x * TILE_SIZE,
        y: selectedGridPos.value.y * TILE_SIZE,
        width: TILE_SIZE,
        height: TILE_SIZE,
        stroke: '#00BFFF', // 鲜艳的蓝色
        strokeWidth: 3,   // 更粗，以示区别
        listening: false,
    }));

    // --- Event Handlers ---
    function handleMouseMove(event: any)
    {
        if (!gameMap.value) return;
        const stage = event.target.getStage();
        const pointerPosition = stage.getPointerPosition();
        if (!pointerPosition)
        {
            handleMouseLeave();
            return;
        }

        const POPOVER_OFFSET = 30;
        popoverTargetPosition.value = {
            x: event.evt.clientX + POPOVER_OFFSET,
            y: event.evt.clientY - POPOVER_OFFSET,
        };

        const gridX = Math.floor(pointerPosition.x / TILE_SIZE);
        const gridY = Math.floor(pointerPosition.y / TILE_SIZE);

        if (gridX < 0 || gridX >= gameMap.value.gridWidth || gridY < 0 || gridY >= gameMap.value.gridHeight)
        {
            handleMouseLeave();
            return;
        }

        if (gridX !== hoveredGridPos.value.x || gridY !== hoveredGridPos.value.y)
        {
            hoveredGridPos.value = {x: gridX, y: gridY};
            hoveredEntities.value = gameMap.value.getEntitiesAtGridPosition(gridX, gridY);
        }

        showPopover.value = true;
    }

    function handleMouseLeave()
    {
        hoveredGridPos.value = {x: -1, y: -1};
        hoveredEntities.value = [];
        showPopover.value = false;
    }

    function handleMouseClick(event: any)
    {
        if (!gameMap.value) return;

        const stage = event.target.getStage();
        const pointerPosition = stage.getPointerPosition();
        if (!pointerPosition) return;

        const gridX = Math.floor(pointerPosition.x / TILE_SIZE);
        const gridY = Math.floor(pointerPosition.y / TILE_SIZE);

        // 点击到地图有效区域之外
        if (gridX < 0 || gridX >= gameMap.value.gridWidth || gridY < 0 || gridY >= gameMap.value.gridHeight)
        {
            // 分支1: 清除地图内部的选中格子状态
            selectedGridPos.value = {x: -1, y: -1};
            // 分支2: 清除 Pinia store 中的选中详情
            selectionStore.clearSelection();
            return;
        }

        // --- 点击在有效区域内 ---

        // 分支1: 更新地图内部的选中格子状态
        selectedGridPos.value = {x: gridX, y: gridY};

        // 分支2: 获取数据、转换并更新 Pinia store
        const entities = gameMap.value.getEntitiesAtGridPosition(gridX, gridY);
        selectionStore.selectEntities(entities);
    }


    return {
        // Hover related
        hoveredGridPos,
        hoveredEntities,
        popoverTargetPosition,
        showPopover,
        hasHoveredData,
        highlightBoxConfig,

        // Selection related
        selectionBoxConfig,
        isACellSelected, // 导出这个computed，用于v-if

        // Interaction (Hover + Select) related
        isHoveringSelectedCell, // 导出这个computed，用于隐藏多余的hover框

        // Event handlers
        handleMouseMove,
        handleMouseLeave,
        handleMouseClick,
    };
}