<template>
  <div ref="mapContainerRef" class="map-container">
    <div v-if="isLoading">Loading...</div>
    <div v-else-if="gameMap" :style="wrapperStyle" class="canvas-wrapper">
      <v-stage
          :config="stageConfig"
          @mousemove="handleMouseMove"
          @mouseleave="handleMouseLeave"
      >
        <!-- 静态渲染层 -->
        <v-layer :config="{ imageSmoothingEnabled: false }">
          <component
              :is="layer.getRendererComponent()"
              v-for="(layer, index) in gameMap.layers"
              :key="index"
              :layer="layer"
          />
        </v-layer>
        <!-- 交互/高亮层 -->
        <v-layer :config="{ imageSmoothingEnabled: false, listening: false }">
          <!-- 高亮框 -->
          <v-rect v-if="showPopover" :config="highlightBoxConfig"/>
          <!-- 高亮对象 -->
          <GameObjectRenderer
              v-for="obj in highlightedObjects"
              :key="`highlight-${obj.id}`"
              :game-object="obj"
              :highlight="true"
          />
        </v-layer>
      </v-stage>
    </div>

    <n-popover
        ref="popoverRef"
        :x="popoverTargetPosition.x"
        :y="popoverTargetPosition.y"
        trigger="manual"
        :show="showPopover"
        placement="top-start"
        :style="{ 'pointer-events': 'none' }"
    >
      <div v-if="hoveredCellInfo" class="cell-info-popover">
        <div class="grid-coords">格子: ({{ hoveredGridPos.x }}, {{ hoveredGridPos.y }})</div>
        <n-divider title-placement="left" style="margin-top: 8px; margin-bottom: 8px;"/>
        <!-- 显示对象 -->
        <div v-for="obj in hoveredCellInfo.objects" :key="obj.id" class="info-item">
          <strong>对象:</strong> {{ obj.type }}
        </div>
        <!-- 显示场 -->
        <div v-for="field in hoveredCellInfo.fields" :key="field.name" class="info-item">
          <strong>场 ({{ field.name }}):</strong> {{ field.value.toFixed(2) }}
        </div>
        <!-- 显示粒子 -->
        <div v-for="p in hoveredCellInfo.particles" :key="p.type" class="info-item">
          <strong>粒子 ({{ p.type }}):</strong> 数量 ~{{ p.count }}
        </div>
        <div v-if="!hasHoveredData" class="info-item-empty">
          空
        </div>
      </div>
    </n-popover>
  </div>
</template>
<script lang="ts" setup>
import {computed, onMounted, ref} from 'vue';
import {TILE_SIZE} from '#/constant';
import {type CellData, FieldLayer, GameMap, ObjectLayer, ParticleLayer, TileLayer} from '#/game/GameMap';
import {createGameObject} from '#/game/GameObjectFactory';
import type {GameObject} from '#/game/GameObject';
import {useElementSize} from '@vueuse/core';
import type {FullLayoutData} from '#/game/types';

// 静态资源导入
import layoutJson from '#/assets/layout.json';
import {registry, kenney_roguelike_rpg_pack} from "#/game/tilesetRegistry.ts";
import GameObjectRenderer from "#/components/GameObjectRenderer.vue";

// --- 响应式尺寸 ---
// 创建一个 ref 来引用模板中的容器元素
const mapContainerRef = ref<HTMLElement | null>(null);
// 使用 useElementSize 实时获取容器的宽高，它们是响应式的 ref
const {width: containerWidth, height: containerHeight} = useElementSize(mapContainerRef);

// --- 组件状态 ---
const isLoading = ref(true);
const gameMap = ref<GameMap | null>(null);

// --- 交互状态 ---
const hoveredGridPos = ref({x: -1, y: -1});
const hoveredCellInfo = ref<CellData | null>(null);
const popoverTargetPosition = ref({x: 0, y: 0});
const showPopover = ref(false);

const highlightedObjects = computed(() => hoveredCellInfo.value?.objects || []);
const hasHoveredData = computed(() =>
    (hoveredCellInfo.value &&
        (hoveredCellInfo.value.objects.length > 0 ||
            hoveredCellInfo.value.fields.length > 0 ||
            hoveredCellInfo.value.particles.length > 0))
);

// --- Konva舞台配置 ---
const stageConfig = computed(() =>
{
  if (!gameMap.value)
  {
    return {width: 0, height: 0};
  }
  // Konva Stage 永远以 1:1 的原始像素尺寸渲染
  return {
    width: gameMap.value.gridWidth * TILE_SIZE,
    height: gameMap.value.gridHeight * TILE_SIZE,
  };
});

// --- 计算CSS包装器的样式 ---
const wrapperStyle = computed(() =>
{
  if (!gameMap.value || containerWidth.value === 0 || containerHeight.value === 0)
  {
    return {};
  }

  const mapPixelWidth = gameMap.value.gridWidth * TILE_SIZE;
  const mapPixelHeight = gameMap.value.gridHeight * TILE_SIZE;

  // 计算缩放比例，逻辑和之前一样
  const scaleX = containerWidth.value / mapPixelWidth;
  const scaleY = containerHeight.value / mapPixelHeight;
  const scale = Math.min(scaleX, scaleY);

  return {
    // 使用 CSS transform 来进行缩放
    transform: `scale(${scale})`,
    // 我们可以设置 transform-origin 来决定缩放的中心点
    'transform-origin': 'center center', // 或者 'center center'
    // 关键：给包装器设置原始尺寸，以便 transform 有正确的基准
    width: `${mapPixelWidth}px`,
    height: `${mapPixelHeight}px`,
  };
});

/**
 * 创建一个带墙壁的矩形地砖布局
 * @param width - 总宽度（网格单位）
 * @param height - 总高度（网格单位）
 * @param wallTileId - 墙壁的瓦片ID
 * @param floorTileId - 地板的瓦片ID
 * @returns 二维数组
 */
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
        row.push(wallTileId); // 边界是墙
      }
      else
      {
        row.push(floorTileId); // 内部是地板
      }
    }
    layout.push(row);
  }
  return layout;
}

// --- 加载和初始化 ---
onMounted(async () =>
{
  try
  {
    // 类型断言，让TypeScript知道我们加载的是新结构
    // @ts-ignore
    const layoutData = layoutJson as FullLayoutData;

    // 1. 加载所有资产
    await registry()

    // 2. 按渲染顺序创建所有图层类的实例
    const layers = [];

    // -- 渲染顺序 1: 背景瓦片 --
    const w = kenney_roguelike_rpg_pack.tileItem.stone_floor, f = kenney_roguelike_rpg_pack.tileItem.wooden_floor;
    const backgroundLayout = createWalledRectangle(
        layoutData.meta.gridWidth,
        layoutData.meta.gridHeight,
        w, f
    );
    layers.push(new TileLayer({
      tilesetId: kenney_roguelike_rpg_pack.id,
      data: backgroundLayout,
    }));

    // -- 渲染顺序 2 (可选): 场图层 --
    // 遍历所有场并为它们创建图层
    for (const fieldName in layoutData.fields)
    {
      layers.push(new FieldLayer({
        name: fieldName,
        data: layoutData.fields[fieldName],
      }));
    }

    // -- 渲染顺序 3 (可选): 粒子图层 --
    // 遍历所有粒子层并为它们创建图层
    for (const particleName in layoutData.particles)
    {
      layers.push(new ParticleLayer(layoutData.particles[particleName]));
    }

    // -- 渲染顺序 4: 实体对象 --
    const gameObjects = layoutData.objects
        .map(createGameObject)
        .filter((o): o is GameObject => o !== null);
    layers.push(new ObjectLayer(gameObjects));

    // 3. 创建 GameMap 实例
    gameMap.value = new GameMap({
      gridWidth: layoutData.meta.gridWidth,
      gridHeight: layoutData.meta.gridHeight,
      layers: layers, // 将有序的图层数组传入
    });

  } catch (error)
  {
    console.error("Failed to initialize map:", error);
  } finally
  {
    isLoading.value = false;
  }
});

const highlightBoxConfig = computed(() => ({
  x: hoveredGridPos.value.x * TILE_SIZE,
  y: hoveredGridPos.value.y * TILE_SIZE,
  width: TILE_SIZE,
  height: TILE_SIZE,
  stroke: '#FFFFAA',
  strokeWidth: 2,
  listening: false,
}));

// --- 事件处理 ---
const handleMouseMove = (event: any) => {
  if (!gameMap.value) return;

  const stage = event.target.getStage();
  // 1. 获取【画布坐标】，用于计算网格位置
  // getPointerPosition() 已经处理了CSS缩放，返回的是舞台内部的1:1坐标
  const pointerPosition = stage.getPointerPosition();
  if (!pointerPosition) {
    handleMouseLeave();
    return;
  }

  // 2. 获取【视口坐标】，用于定位 Popover
  // event.evt 是原始的DOM事件
  const POPOVER_OFFSET = 30;
  popoverTargetPosition.value = {
    x: event.evt.clientX + POPOVER_OFFSET,
    y: event.evt.clientY - POPOVER_OFFSET
  };

  // 3. 计算网格坐标，【不需要】除以 scale
  const gridX = Math.floor(pointerPosition.x / TILE_SIZE);
  const gridY = Math.floor(pointerPosition.y / TILE_SIZE);

  // 检查是否移出有效区域
  if (gridX < 0 || gridX >= gameMap.value.gridWidth || gridY < 0 || gridY >= gameMap.value.gridHeight) {
    handleMouseLeave();
    return;
  }

  // 只有当格子变化时才重新获取数据，避免不必要的计算
  if (gridX !== hoveredGridPos.value.x || gridY !== hoveredGridPos.value.y) {
    hoveredGridPos.value = { x: gridX, y: gridY };
    hoveredCellInfo.value = gameMap.value.getDataAtGridPosition(gridX, gridY);
  }

  showPopover.value = true;
};

const handleMouseLeave = () => {
  hoveredGridPos.value = {x: -1, y: -1};
  hoveredCellInfo.value = null;
  showPopover.value = false;
};
</script>

<style scoped>
/* 确保容器撑满其父元素，为尺寸计算提供基础 */
.map-container {
  width: 100%;
  height: 100%;
  min-height: 100px; /* 可以给一个最小高度，避免初始渲染时高度为0 */
  display: flex;
  justify-content: center;
  align-items: center;
  background-color: #1a1a1a; /* 给个背景色，方便观察 TODO 之后改成 Naive的主题色 */
  overflow: hidden; /* 防止缩放后的canvas溢出 */
}

.cell-info-popover {
  min-width: 180px;
  font-size: 13px;
}

.grid-coords {
  font-weight: bold;
  color: #9FEAF9;
}

.info-item {
  margin-bottom: 4px;
}

.info-item-empty {
  color: #888;
  font-style: italic;
}
</style>