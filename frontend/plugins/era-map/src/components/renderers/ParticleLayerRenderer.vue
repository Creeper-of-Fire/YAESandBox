<!-- src/components/renderers/ParticleLayerRenderer.vue -->
<template>
  <v-shape :config="shapeConfig" />
</template>

<script lang="ts" setup>
// 它接收一个 ParticleLayer 类型的prop，并使用其seed和densityGrid
// 在 v-shape 的 sceneFunc 中高效绘制所有粒子。
import { computed, onMounted, ref } from 'vue';
import { TILE_SIZE } from '#/constant';
import type { Context } from 'konva/lib/Context';
import {ParticleLayer} from "#/game-logic/entity/particle/render/ParticleLayer.ts";

// 伪随机数生成器 (PRNG)
function createPRNG(seed: number) {
  return function() {
    let t = seed += 0x6D2B79F5;
    t = Math.imul(t ^ t >>> 15, t | 1);
    t ^= t + Math.imul(t ^ t >>> 7, t | 61);
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  }
}

const props = defineProps<{
  layer: ParticleLayer;
}>();

const particles = ref<{x: number, y: number, radius: number, color: string}[]>([]);

onMounted(() => {
  const { seed, densityGrid, particleConfig } = props.layer.data;

  const random = createPRNG(seed);
  const generatedParticles = [];

  const color1 = particleConfig.colorRange[0];
  const [minSize, maxSize] = particleConfig.sizeRange;

  for (let x = 0; x < densityGrid.length; x++) {
    for (let y = 0; y < densityGrid[x].length; y++) {
      const count = densityGrid[x][y];
      for (let i = 0; i < count; i++) {
        const particleX = (x + random()) * TILE_SIZE;
        const particleY = (y + random()) * TILE_SIZE;
        const radius = minSize + (maxSize - minSize) * random();

        generatedParticles.push({
          x: particleX,
          y: particleY,
          radius: radius,
          color: color1,
        });
      }
    }
  }
  particles.value = generatedParticles;
});

const shapeConfig = computed(() => ({
  sceneFunc: (context: Context, shape: any) => {
    for (const p of particles.value) {
      context.beginPath();
      context.arc(p.x, p.y, p.radius, 0, Math.PI * 2, false);
      context.fillStyle = p.color;
      context.fill();
    }
    context.fillShape(shape);
  },
  listening: false,
}));
</script>