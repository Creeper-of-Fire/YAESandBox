<template>
  <div v-if="isLoading">Loading Map Assets...</div>
  <!-- 使用tavernMap实例来驱动渲染 -->
  <v-stage v-else-if="tavernMap" :config="stageConfig">
    <v-layer>
      <!-- 渲染地图背景瓦片 -->
      <template v-for="(row, y) in tavernMap.layout" :key="`row-${y}`">
        <template v-for="(tileId, x) in row" :key="`tile-${y}-${x}`">
          <v-image
              v-if="tileId >= 0"
              :config="{
                x: x * TILE_SIZE,
                y: y * TILE_SIZE,
                image: tavernMap.tileset.image,
                width: TILE_SIZE,
                height: TILE_SIZE,
                crop: tavernMap.tileset.getTileCrop(tileId),
              }"
          />
        </template>
      </template>

      <!-- 渲染所有游戏对象 -->
      <GameObjectRenderer
          v-for="obj in tavernMap.gameObjects"
          :key="obj.id"
          :game-object="obj"
          :tileset="tavernMap.tileset"
      />
    </v-layer>
  </v-stage>
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';
import { TILE_SIZE } from '#/constant';
import { TavernMap } from '#/game/TavernMap'; // 引入我们的新模型
import GameObjectRenderer from "#/components/GameObjectRenderer.vue";

// 静态资源导入
// @ts-ignore
import tilesetAssetUrl from '#/assets/tilemap_packed.png';
import layoutJson from '#/assets/layout.json';

// 将资产配置集中管理
const ASSET_CONFIG = {
  layoutData: layoutJson,
  tileset: {
    url: tilesetAssetUrl,
    sourceTileSize: 16,
    columns: 12,
  }
};

// TODO 之后要实现画布大小的自适应
const MAP_SCALE = 0.5

// --- 组件状态 ---
const tavernMap = ref<TavernMap | null>(null);
const isLoading = ref(true);

// --- Konva舞台配置 ---
const stageConfig = computed(() => {
  if (!tavernMap.value) return { width: 0, height: 0 };

  const mapPixelWidth = tavernMap.value.gridWidth * TILE_SIZE;
  const mapPixelHeight = tavernMap.value.gridHeight * TILE_SIZE;

  return {
    width: mapPixelWidth,
    height: mapPixelHeight,
    scaleX: MAP_SCALE,
    scaleY: MAP_SCALE,
  };
});

// --- 加载和初始化 ---
onMounted(async () => {
  try {
    // 组件的职责就是调用工厂方法，获取一个完全配置好的模型实例
    tavernMap.value = await TavernMap.create(ASSET_CONFIG);
  } catch (error) {
    console.error('Failed to initialize TavernMap:', error);
  } finally {
    isLoading.value = false;
  }
});
</script>