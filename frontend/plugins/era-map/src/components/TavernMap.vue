<template>
  <div v-if="isLoading">Loading Map Assets...</div>
  <v-stage  v-else-if="scene"  :config="stageConfig">
    <v-layer>
      <!-- 遍历地图数据来渲染每一块瓦片 -->
<!--      <template v-for="(row, y) in mapLayout" :key="`row-${y}`">-->
<!--        <template v-for="(tileId, x) in row" :key="`tile-${y}-${x}`">-->
<!--          <v-image-->
<!--              :config="{-->
<!--              x: x * TILE_SIZE,-->
<!--              y: y * TILE_SIZE,-->
<!--              image: tilesetImage,-->
<!--              width: TILE_SIZE,-->
<!--              height: TILE_SIZE,-->
<!--              // crop是关键：从图集中裁剪出我们需要的那个瓦片-->
<!--              crop: getTileCrop(tileId),-->
<!--            }"-->
<!--          />-->
<!--        </template>-->
<!--      </template>-->
      <!-- 渲染所有游戏对象 -->
      <GameObjectRenderer
          v-for="obj in scene.gameObjects"
          :key="obj.id"
          :game-object="obj"
          :tileset-image="tilesetImage"
      />
    </v-layer>
  </v-stage>
</template>

<script lang="ts" setup>
import {computed, onMounted, ref} from 'vue';
// @ts-ignore
import tilesetAssetUrl from '#/assets/tilemap_packed.png'
// @ts-ignore
import layoutJson from '#/assets/layout.json'
import type {RawGameObjectData} from "#/game/types.ts";
import {createGameObject} from '#/game/GameObjectFactory';
import type {GameObject} from "#/game/GameObject.ts";
import { TILE_SIZE,SPIRTE_TILE_SIZE } from '#/constant';
import GameObjectRenderer from "#/components/GameObjectRenderer.vue";
// 假设我们的酒馆是 10x8 的大小
// 0: 地板
// 1: 墙壁
const w = 40;
const T = 72;
const tavernMapLayout: number[][] = [
  [w, w, w, w, w, w, w, w, w, w],
  [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
  [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
  [w, 0, 0, T, 0, 0, 0, 0, 0, w],
  [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
  [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
  [w, 0, 0, 0, 0, 0, 0, 0, 0, w],
  [w, w, w, w, w, w, w, w, w, w],
];

// --- 配置项 ---
const TILESET_URL = tilesetAssetUrl;
const MAP_SCALE = 3;

// --- 场景状态 ---
interface Scene
{
  gridWidth: number;
  gridHeight: number;
  gameObjects: GameObject[];
}

const scene = ref<Scene | null>(null);
const isLoading = ref(true);
const tilesetImage = ref<HTMLImageElement | null>(null);

// --- Konva舞台配置 ---
const stageConfig = computed(() =>
{
  if (!scene.value) return {width: 0, height: 0};

  const mapPixelWidth = scene.value.gridWidth * SPIRTE_TILE_SIZE;
  const mapPixelHeight = scene.value.gridHeight * SPIRTE_TILE_SIZE;

  return {
    width: mapPixelWidth * MAP_SCALE,
    height: mapPixelHeight * MAP_SCALE,
    scaleX: MAP_SCALE,
    scaleY: MAP_SCALE,
  };
});

// --- 加载和初始化 ---
onMounted(async () =>
{
  try
  {
    // 并行加载图片和JSON数据
    const [imageData, layoutData] = await Promise.all([
      loadTilesetImage(),
      layoutJson,
    ]);

    tilesetImage.value = imageData;
    const rawGameObject = layoutData.objects;

    // 使用工厂模式创建所有游戏对象
    const gameObjects = rawGameObject
        .map((raw: RawGameObjectData) => createGameObject(raw))
        .filter((obj:any): obj is GameObject => obj !== null);

    // 更新场景
    scene.value = {
      gridWidth: layoutData.meta.gridWidth,
      gridHeight: layoutData.meta.gridHeight,
      gameObjects,
    };

  } catch (error)
  {
    console.error('Failed to initialize scene:', error);
    // 可以在这里设置错误状态
  } finally
  {
    isLoading.value = false;
  }
});

// --- 工具函数 ---
function loadTilesetImage(): Promise<HTMLImageElement>
{
  return new Promise((resolve, reject) =>
  {
    const image = new window.Image();
    image.src = TILESET_URL;
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('Failed to load tileset image.'));
  });
}

async function loadLayoutData(url: string): Promise<any>
{
  const response = await fetch(url);
  if (!response.ok)
  {
    throw new Error(`Failed to fetch layout data from ${url}`);
  }
  return response.json();
}
</script>