<template>
  <!--
    这个组件是一个纯粹的渲染器。
    它不包含任何业务逻辑，只负责将 TileLayer 数据模型转换为 Konva 的 v-image 组件。
  -->
  <template v-for="(row, y) in layer.data" :key="`row-${y}`">
    <template v-for="(tileId, x) in row" :key="`tile-${y}-${x}`">
      <!--
        我们只渲染有效的瓦片 (ID >= 0)。
        -1 或其他负数通常用作“空”或“透明”瓦片的标记。
      -->
      <v-image
          v-if="tileId >= 0"
          :config="getTileConfig(tileId, x, y)"
      />
    </template>
  </template>
</template>

<script lang="ts" setup>
import { toRefs } from 'vue';
import type { TileLayer } from '#/game/GameMap';
import { resourceManager } from '#/game/ResourceManager';
import { TILE_SIZE } from '#/constant';

// 1. 定义Props：明确声明该组件需要一个类型为 TileLayer 的 layer 属性。
//    这提供了强大的类型安全保证。
const props = defineProps<{
  layer: TileLayer;
}>();

// 使用 toRefs 保持响应性，虽然在这里 layer 本身不会改变，但这是个好习惯。
const { layer } = toRefs(props);

// 2. 核心辅助函数：为给定的瓦片计算Konva的配置对象。
function getTileConfig(tileId: number, x: number, y: number) {
  // 从资源管理器中按ID获取图集。这是解耦的关键。
  // 我们不再需要通过 props 层层传递 tileset 对象。
  const tileset = resourceManager.getTileset(layer.value.tilesetId)!;

  return {
    // 渲染位置（像素坐标）
    x: x * TILE_SIZE,
    y: y * TILE_SIZE,
    // 源图像
    image: tileset.image,
    // 渲染尺寸
    width: TILE_SIZE,
    height: TILE_SIZE,
    // 从图集中裁剪出正确的瓦片
    crop: tileset.getTileCrop(tileId),
  };
}
</script>