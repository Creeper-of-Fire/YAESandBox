<template>
  <div ref="mapContainerRef" class="map-container">
    <div v-if="gameMap" :style="wrapperStyle" class="canvas-wrapper">
      <v-stage :config="stageConfig">
        <!-- 渲染所有图层 -->
        <v-layer v-for="(layer, index) in gameMap.layers" :key="`layer-${index}`" :config="{ imageSmoothingEnabled: false }">
          <component
              :is="layer.getRendererComponent()"
              v-for="(layer, index) in gameMap.layers"
              :key="index"
              :layer="layer"
          />
        </v-layer>
      </v-stage>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {computed, ref, toRefs} from 'vue';
import {useElementSize} from '@vueuse/core';

// 导入新的逻辑模型和旧的渲染模型
import {LogicalGameMap} from '#/game-logic/LogicalGameMap';
import {GameObjectRender} from '#/game-render/GameObjectRender';

// 导入渲染组件
import {TILE_SIZE} from '#/constant';

const props = defineProps<{
  gameMap: LogicalGameMap | null;
}>();

const {gameMap} = toRefs(props);
const mapContainerRef = ref<HTMLElement | null>(null);
const {width: containerWidth, height: containerHeight} = useElementSize(mapContainerRef);

// --- Konva 和 CSS 缩放逻辑 (与旧 TavernMap 相同) ---
const stageConfig = computed(() =>
{
  if (!gameMap.value) return {width: 0, height: 0};
  return {
    width: gameMap.value.gridWidth * TILE_SIZE,
    height: gameMap.value.gridHeight * TILE_SIZE,
  };
});

const wrapperStyle = computed(() =>
{
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
</script>

<style scoped>
.map-container {
  width: 100%;
  height: 100%;
  display: flex;
  justify-content: center;
  align-items: center;
  overflow: hidden;
}
</style>