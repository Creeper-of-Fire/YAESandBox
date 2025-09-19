<template>
  <div ref="mapContainerRef" class="map-container">
    <div v-if="gameMap" :style="wrapperStyle" class="canvas-wrapper">
      <v-stage :config="stageConfig" @click="handleCanvasClick">
        <!-- 渲染所有图层 -->
        <v-layer v-for="(layer, index) in gameMap.layers" :key="`layer-${index}`" :config="{ imageSmoothingEnabled: false }">

          <!-- 瓦片层渲染器 -->
          <template v-if="layer instanceof TileLayer">
            <TileLayerRenderer :layer="layer" />
          </template>

          <!-- 逻辑对象层渲染器 -->
          <template v-if="layer instanceof LogicalObjectLayer">
            <!-- 遍历逻辑对象，并将其 renderInfo 传递给渲染组件 -->
            <GameObjectRenderer
                v-for="obj in layer.objects"
                :key="obj.id"
                :game-object="obj.renderInfo"
            />
          </template>

          <!-- 其他图层渲染器 (Field, Particle)... -->

        </v-layer>
      </v-stage>
    </div>
  </div>
</template>

<script lang="ts" setup>
import { computed, ref, toRefs } from 'vue';
import { useElementSize } from '@vueuse/core';
import type Konva from 'konva';

// 导入新的逻辑模型和旧的渲染模型
import { LogicalGameMap, LogicalObjectLayer } from '#/game-logic/LogicalGameMap';
import { TileLayer } from '#/game-render/GameMap'; // 假设路径正确
import { GameObjectRender } from '#/game-render/GameObjectRender';

// 导入渲染组件
import TileLayerRenderer from '#/components/renderers/TileLayerRenderer.vue';
import GameObjectRenderer from '#/components/GameObjectRenderer.vue';
import { TILE_SIZE } from '#/constant';

const props = defineProps<{
  gameMap: LogicalGameMap | null;
}>();

const emit = defineEmits<{
  (e: 'object-click', objectId: string): void
}>();

const { gameMap } = toRefs(props);
const mapContainerRef = ref<HTMLElement | null>(null);
const { width: containerWidth, height: containerHeight } = useElementSize(mapContainerRef);

// --- Konva 和 CSS 缩放逻辑 (与旧 TavernMap 相同) ---
const stageConfig = computed(() => {
  if (!gameMap.value) return { width: 0, height: 0 };
  return {
    width: gameMap.value.gridWidth * TILE_SIZE,
    height: gameMap.value.gridHeight * TILE_SIZE,
  };
});

const wrapperStyle = computed(() => {
  if (!gameMap.value || !containerWidth.value || !containerHeight.value) return {};
  const mapPixelWidth = gameMap.value.gridWidth * TILE_SIZE;
  const mapPixelHeight = gameMap.value.gridHeight * TILE_SIZE;
  const scale = Math.min(containerWidth.value / mapPixelWidth, containerHeight.value / mapPixelHeight);
  return {
    transform: `scale(${scale})`,
    'transform-origin': 'center center',
    width: `${mapPixelWidth}px`,
    height: `${mapPixelHeight}px`,
  };
});

// --- 交互逻辑 ---
function handleCanvasClick(event: Konva.KonvaEventObject<MouseEvent>) {
  // Konva 的事件目标是实际点击的 Shape/Image，它的 name 属性通常是 ID
  // 我们在 GameObjectRenderer 中可能需要设置一下 name 属性
  const targetShape = event.target;

  // 向上追溯，直到找到一个 group，这个 group 代表一个 GameObjectRender
  let targetGroup = targetShape;
  while (targetGroup.getParent() && !(targetGroup.attrs.id)) {
    targetGroup = targetGroup.getParent();
  }

  const objectId = targetGroup.attrs.id;

  if (objectId) {
    emit('object-click', objectId);
  }
}

// HACK: 为了让点击生效，我们需要修改 GameObjectRenderer.vue
// 在 v-group 的 groupConfig 中增加 `id: gameObject.value.id`
</script>

<style scoped>
.map-container {
  width: 100%;
  height: 100%;
  display: flex;
  justify-content: center;
  align-items: center;
  background-color: #2c2c2c;
  overflow: hidden;
}
.canvas-wrapper {
  /* ... */
}
</style>