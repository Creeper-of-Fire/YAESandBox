<!-- src/components/renderers/FieldLayerRenderer.vue -->
<template>
  <v-image :config="imageConfig" />
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';
import type { FieldLayer } from '#/game/GameMap';
import { TILE_SIZE } from '#/constant';

const props = defineProps<{
  layer: FieldLayer;
}>();

const imageRef = ref<HTMLCanvasElement | null>(null);

// 在组件挂载时，一次性将场数据绘制到离屏Canvas上
onMounted(() => {
  const { data, name } = props.layer;
  if (!data || data.length === 0) return;

  const width = data.length;
  const height = data[0].length;

  const canvas = document.createElement('canvas');
  canvas.width = width * TILE_SIZE;
  canvas.height = height * TILE_SIZE;
  const context = canvas.getContext('2d')!;

  // 示例：渲染光照场。我们可以根据场名称选择不同的渲染策略
  // TODO 之后可以改成数据驱动的
  if (name === 'light_level') {
    for (let x = 0; x < width; x++) {
      for (let y = 0; y < height; y++) {
        const intensity = data[x][y];
        // 将光照强度转换为黑色遮罩的透明度
        // intensity=1 -> a=0 (全亮)
        // intensity=0 -> a=1 (全黑)
        context.fillStyle = `rgba(0, 0, 0, ${1 - intensity})`;
        context.fillRect(x * TILE_SIZE, y * TILE_SIZE, TILE_SIZE, TILE_SIZE);
      }
    }
  }
  // 可以在这里添加 else if 来处理其他类型的场，如温度等

  imageRef.value = canvas;
});

const imageConfig = computed(() => ({
  image: imageRef.value,
  x: 0,
  y: 0,
  listening: false, // 场通常不需要交互
  opacity: 0.6, // 可以给一个全局的透明度，让它看起来更像一个叠加效果
}));
</script>