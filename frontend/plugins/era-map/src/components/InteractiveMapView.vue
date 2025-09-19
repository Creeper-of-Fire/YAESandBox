<template>
  <div ref="mapContainerRef" class="map-container">
    <div v-if="gameMap" :style="wrapperStyle" class="canvas-wrapper">
      <v-stage
          :config="stageConfig"
          @mousemove="handleMouseMove"
          @click="handleMouseClick"
          @mouseleave="handleMouseLeave"
      >
        <!-- 渲染所有图层 -->
        <v-layer v-for="(layer, index) in gameMap.layers" :key="`layer-${index}`" :config="{ imageSmoothingEnabled: false }">
          <component
              :is="layer.getRendererComponent()"
              v-for="(layer, index) in gameMap.layers"
              :key="index"
              :layer="layer"
          />
        </v-layer>

        <!-- 交互与高亮层 (始终在最顶层) -->
        <v-layer :config="{ listening: false }">
          <!-- 悬浮高亮框 (黄色) -->
          <v-rect v-if="showPopover" :config="highlightBoxConfig"/>
          <!-- 选中状态框 (蓝色) -->
          <v-rect v-if="isACellSelected" :config="selectionBoxConfig"/>

          <!-- 真正的高亮对象渲染 -->
          <!-- 这个 v-for 只会遍历当前悬浮的少量实体，性能极高 -->
          <template v-for="entity in hoveredEntities" :key="`highlight-${entity.id}`">
            <GameObjectRenderer
                v-if="entity instanceof GameObjectEntity"
                :game-object="entity"
                :highlight="true"
            />
            <!-- 以后可以扩展到高亮 Field 等 -->
            <!-- <FieldRenderer v-if="entity instanceof FieldEntity" :field="entity" :highlight="true" /> -->
          </template>
        </v-layer>
      </v-stage>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {computed, ref, toRefs} from 'vue';
import {useElementSize} from '@vueuse/core';

// 导入新的逻辑模型和旧的渲染模型
import {GameMap} from '#/game-logic/GameMap.ts';

// 导入渲染组件
import {TILE_SIZE} from '#/constant.ts';
import {useMapInteraction} from '#/composables/useMapInteraction.ts';
import {GameObjectEntity} from "#/game-logic/entity/gameObject/GameObjectEntity.ts";
import GameObjectRenderer from "#/components/GameObjectRenderer.vue";

const props = defineProps<{
  gameMap: GameMap | null;
}>();

const {gameMap} = toRefs(props);
const mapContainerRef = ref<HTMLElement | null>(null);
const {width: containerWidth, height: containerHeight} = useElementSize(mapContainerRef);

// --- Konva 和 CSS 缩放逻辑 ---
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

// --- 使用 Composable 管理交互 ---
const {
  // Hover
  hoveredGridPos,
  hoveredEntities,
  popoverTargetPosition,
  showPopover,
  hasHoveredData,
  highlightBoxConfig,

  // Selection
  selectionBoxConfig,
  isACellSelected,

  // Interaction
  isHoveringSelectedCell,

  // Handlers
  handleMouseMove,
  handleMouseLeave,
  handleMouseClick,
} = useMapInteraction(gameMap);

const highlightedEntities = computed(() =>
{
  if (!gameMap.value || hoveredGridPos.value.x < 0)
  {
    return [];
  }
  return gameMap.value.getEntitiesAtGridPosition(
      hoveredGridPos.value.x,
      hoveredGridPos.value.y
  );
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