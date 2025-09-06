<template>
  <div v-if="isLoading">Loading Map Assets...</div>
  <v-stage v-else :config="stageConfig">
    <v-layer>
      <!-- 遍历地图数据来渲染每一块瓦片 -->
      <template v-for="(row, y) in mapLayout" :key="`row-${y}`">
        <template v-for="(tileId, x) in row" :key="`tile-${y}-${x}`">
          <v-image
              :config="{
              x: x * TILE_SIZE,
              y: y * TILE_SIZE,
              image: tilesetImage,
              width: TILE_SIZE,
              height: TILE_SIZE,
              // crop是关键：从图集中裁剪出我们需要的那个瓦片
              crop: getTileCrop(tileId),
            }"
          />
        </template>
      </template>
    </v-layer>
  </v-stage>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
// @ts-ignore
import tilesetAssetUrl from '#/assets/tilemap_packed.png'
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
const TILE_SIZE = 16; // 每个瓦片的像素尺寸
const TILESET_COLUMNS = 12; // 你的图集文件每行有多少个瓦片
const MAP_SCALE = 3;

// --- 响应式状态 ---
const isLoading = ref(true);
const tilesetImage = ref<HTMLImageElement | null>(null);
const mapLayout = ref(tavernMapLayout);

// --- Konva舞台配置 ---
const stageConfig = computed(() => {
  const mapWidth = mapLayout.value[0].length * TILE_SIZE;
  const mapHeight = mapLayout.value.length * TILE_SIZE;
  return {
    // 舞台的最终渲染尺寸
    width: mapWidth * MAP_SCALE,
    height: mapHeight * MAP_SCALE,
    // 告诉舞台将其内部所有内容放大
    scaleX: MAP_SCALE,
    scaleY: MAP_SCALE,
  };
});

// --- 核心逻辑：加载图集并在加载完成后更新状态 ---
onMounted(() => {
  const image = new window.Image();
  image.src = TILESET_URL;
  image.onload = () => {
    tilesetImage.value = image;
    isLoading.value = false;
  };
  image.onerror = () => {
    console.error('Failed to load tileset image.');
    isLoading.value = false; // 也可以设置一个错误状态
  };
});

// --- 核心工具函数：根据瓦片ID计算裁剪区域 ---
function getTileCrop(tileId: number) {
  // 计算瓦片在图集中的坐标 (x, y)
  const tileX = tileId % TILESET_COLUMNS;
  const tileY = Math.floor(tileId / TILESET_COLUMNS);

  return {
    x: tileX * TILE_SIZE,
    y: tileY * TILE_SIZE,
    width: TILE_SIZE,
    height: TILE_SIZE,
  };
}
</script>