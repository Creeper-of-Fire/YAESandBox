<template>
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
</template>
<script lang="ts" setup>
import {computed, onMounted, ref} from 'vue';
import {TILE_SIZE} from '#/constant';
import {resourceManager} from '#/game/ResourceManager';
import {GameMap, ObjectLayer, TileLayer} from '#/game/GameMap';
import {createGameObject} from '#/game/GameObjectFactory';
import type {GameObject} from '#/game/GameObject';

// 静态资源导入
// @ts-ignore
import tilesetAssetUrl from '#/assets/tilemap_packed.png';
import layoutJson from '#/assets/layout.json';

// TODO 之后要实现画布大小的自适应
const MAP_SCALE = 0.5

// --- 组件状态 ---
const isLoading = ref(true);
const gameMap = ref<GameMap | null>(null);

// --- Konva舞台配置 ---
const stageConfig = computed(() =>
{
  if (!gameMap.value) return {width: 0, height: 0};
  return {
    width: gameMap.value.gridWidth * TILE_SIZE,
    height: gameMap.value.gridHeight * TILE_SIZE,
    scaleX: MAP_SCALE,
    scaleY: MAP_SCALE,
  };
});

function getTileConfig(tilesetId: string, tileId: number, x: number, y: number)
{
  const tileset = resourceManager.getTileset(tilesetId)!;
  return {
    x: x * TILE_SIZE,
    y: y * TILE_SIZE,
    image: tileset.image,
    width: TILE_SIZE,
    height: TILE_SIZE,
    crop: tileset.getTileCrop(tileId),
  };
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
    const w = 40;
    const hardcodedLayout = [
      [w, w, w, w, w, w, w, w, w, w],
      [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
      [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
      [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
      [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
      [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
      [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
      [w, w, w, w, w, w, w, w, w, w],
    ];
    const backgroundLayer = new TileLayer({
      tilesetId: 'terrain_main',
      data: hardcodedLayout,
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