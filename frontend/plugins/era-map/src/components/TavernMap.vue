<template>
  <div ref="mapContainerRef" class="map-container">
    <div v-if="isLoading">Loading...</div>
    <v-stage v-else-if="gameMap" :config="stageConfig">
      <v-layer>
        <component
            :is="layer.getRendererComponent()"
            v-for="(layer, index) in gameMap.layers"
            :key="index"
            :layer="layer"
        />
      </v-layer>
    </v-stage>
  </div>
</template>
<script lang="ts" setup>
import {computed, onMounted, ref} from 'vue';
import {TILE_SIZE} from '#/constant';
import {resourceManager} from '#/game/ResourceManager';
import {GameMap, ObjectLayer, TileLayer} from '#/game/GameMap';
import {createGameObject} from '#/game/GameObjectFactory';
import type {GameObject} from '#/game/GameObject';
import { useElementSize } from '@vueuse/core';

// 静态资源导入
// @ts-ignore
import tilesetAssetUrl from '#/assets/tilemap_packed.png';
import layoutJson from '#/assets/layout.json';

// --- 响应式尺寸 ---
// 创建一个 ref 来引用模板中的容器元素
const mapContainerRef = ref<HTMLElement | null>(null);
// 使用 useElementSize 实时获取容器的宽高，它们是响应式的 ref
const { width: containerWidth, height: containerHeight } = useElementSize(mapContainerRef);

// --- 组件状态 ---
const isLoading = ref(true);
const gameMap = ref<GameMap | null>(null);

// --- Konva舞台配置 ---
const stageConfig = computed(() =>
{
  if (!gameMap.value || containerWidth.value === 0 || containerHeight.value === 0) {
    return {width: 0, height: 0};
  }

  // 地图的原始像素尺寸
  const mapPixelWidth = gameMap.value.gridWidth * TILE_SIZE;
  const mapPixelHeight = gameMap.value.gridHeight * TILE_SIZE;

  // 计算缩放比例，确保地图能完整地显示在容器内
  const scaleX = containerWidth.value / mapPixelWidth;
  const scaleY = containerHeight.value / mapPixelHeight;
  const scale = Math.min(scaleX, scaleY); // 取较小的比例，保证长宽都在容器内

  return {
    // Konva Stage的尺寸应该等于容器的尺寸
    width: containerWidth.value,
    height: containerHeight.value,
    // 动态计算缩放
    scaleX: scale,
    scaleY: scale,
    // [可选优化] 将地图居中显示
    offsetX: - (containerWidth.value / scale - mapPixelWidth) / 2,
    offsetY: - (containerHeight.value / scale - mapPixelHeight) / 2,
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
function createWalledRectangle(width: number, height: number, wallTileId: number, floorTileId: number): number[][] {
  const layout: number[][] = [];
  for (let y = 0; y < height; y++) {
    const row: number[] = [];
    for (let x = 0; x < width; x++) {
      if (y === 0 || y === height - 1 || x === 0 || x === width - 1) {
        row.push(wallTileId); // 边界是墙
      } else {
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
    // 1. 加载所有资产
    await resourceManager.loadTileset({
      id: 'terrain_main',
      url: tilesetAssetUrl,
      sourceTileSize: 16,
      columns: 12,
    });
    // 可以在这里加载更多图集, e.g. await resourceManager.loadTileset({ id: 'characters', ... })

    // 2. 创建图层和GameMap
    const w = 40; // 墙壁ID
    const f = 0;  // 地板ID
    const backgroundLayout = createWalledRectangle(
        layoutJson.meta.gridWidth,
        layoutJson.meta.gridHeight,
        w,
        f
    );
    const backgroundLayer = new TileLayer({
      tilesetId: 'terrain_main',
      data: backgroundLayout,
    });

    const gameObjects = layoutJson.objects
        .map(createGameObject)
        .filter((o): o is GameObject => o !== null);
    const objectLayer = new ObjectLayer(gameObjects);

    gameMap.value = new GameMap({
      gridWidth: layoutJson.meta.gridWidth,
      gridHeight: layoutJson.meta.gridHeight,
      layers: [backgroundLayer, objectLayer], // 按渲染顺序放入数组
    });

  } catch (error)
  {
    console.error("Failed to initialize map:", error);
  } finally
  {
    isLoading.value = false;
  }
});
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
}
</style>