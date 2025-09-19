<!-- src/components/renderers/FieldLayerRenderer.vue -->
<template>
  <v-image :config="imageConfig" />
</template>

<script lang="ts" setup>
import { computed, onMounted, ref,toRefs,watch } from 'vue';
import { TILE_SIZE } from '#/constant';
import {FieldContainerLayer} from "#/game-logic/entity/field/render/FieldContainerLayer.ts";
import type {FieldEntity} from "#/game-logic/entity/field/FieldEntity.ts";
import {useWorldStateStore} from "#/stores/useWorldStateStore.ts";

const props = defineProps<{
  layer: FieldContainerLayer;
}>();
const { layer } = toRefs(props);
const imageRef = ref<HTMLCanvasElement | null>(null);

// 辅助函数：定义不同场的渲染策略
function renderField(context: CanvasRenderingContext2D, entity: FieldEntity, width: number, height: number) {
  const { name, data } = entity;

  // 我们可以根据场名称选择不同的渲染策略
  if (name === 'light_level') {
    for (let x = 0; x < width; x++) {
      for (let y = 0; y < height; y++) {
        const intensity = data[x]?.[y] ?? 0;
        // 将光照强度转换为黑色遮罩的透明度
        // intensity=1 -> alpha=0 (全亮)
        // intensity=0 -> alpha=1 (全黑)
        context.fillStyle = `rgba(0, 0, 0, ${1 - intensity})`;
        context.fillRect(x * TILE_SIZE, y * TILE_SIZE, TILE_SIZE, TILE_SIZE);
      }
    }
  }
  // 未来可以在这里添加 else if 来处理其他类型的场，如温度（渲染为红蓝色调的热图）等
  // TODO 又或者使用fieldRegistry定义的方式来处理场
  // else if (name === 'temperature') { ... }
}

// 核心渲染逻辑
function drawFieldsToCanvas() {
  if (layer.value.entities.length === 0) {
    imageRef.value = null;
    return;
  }

  // 从 world state 获取地图尺寸，这是更可靠的数据源
  const worldState = useWorldStateStore();
  const gridWidth = worldState.logicalGameMap?.gridWidth;
  const gridHeight = worldState.logicalGameMap?.gridHeight;

  if (!gridWidth || !gridHeight) return;

  const canvas = document.createElement('canvas');
  canvas.width = gridWidth * TILE_SIZE;
  canvas.height = gridHeight * TILE_SIZE;
  const context = canvas.getContext('2d')!;

  // 遍历容器层中的所有场实体，并将它们依次绘制到同一个 canvas 上
  for (const entity of layer.value.entities) {
    renderField(context, entity, gridWidth, gridHeight);
  }

  imageRef.value = canvas;
}

// 在组件挂载时进行首次绘制
onMounted(drawFieldsToCanvas);

// 如果场的实体列表可能发生变化（例如动态添加/移除场），可以添加一个监听器
watch(() => layer.value.entities, drawFieldsToCanvas, { deep: true });

const imageConfig = computed(() => ({
  image: imageRef.value,
  x: 0,
  y: 0,
  listening: false, // 场通常不需要交互
  opacity: 0.6, // 可以给一个全局的透明度，让它看起来更像一个叠加效果
}));
</script>