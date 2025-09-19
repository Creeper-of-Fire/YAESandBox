<!-- src/components/renderers/ParticleLayerRenderer.vue -->
<template>
  <v-shape :config="shapeConfig"/>
</template>

<script lang="ts" setup>
// 它接收一个 ParticleLayer 类型的prop，并使用其seed和densityGrid
// 在 v-shape 的 sceneFunc 中高效绘制所有粒子。
import {computed, onMounted, ref, toRefs} from 'vue';
import {TILE_SIZE} from '#/constant';
import type {Context} from 'konva/lib/Context';
import {ParticleContainerLayer} from "#/game-logic/entity/particle/render/ParticleContainerLayer.ts";
import {particleRegistry} from '#/game-logic/entity/particle/render/particleRegistry';

// 伪随机数生成器 (PRNG)
function createPRNG(seed: number)
{
  return function ()
  {
    let t = seed += 0x6D2B79F5;
    t = Math.imul(t ^ t >>> 15, t | 1);
    t ^= t + Math.imul(t ^ t >>> 7, t | 61);
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  }
}

const props = defineProps<{
  layer: ParticleContainerLayer;
}>();
const {layer} = toRefs(props);
// 粒子数据现在是一个扁平数组，包含所有实体生成的粒子
const allParticles = ref<{ x: number, y: number, radius: number, color: string }[]>([]);

// 粒子生成逻辑
function generateAllParticles()
{
  const generatedParticles = [];

  // 遍历容器中的每个粒子实体
  for (const entity of layer.value.entities)
  {
    const {seed, densityGrid, type} = entity.data;
    const config = particleRegistry[type]; // 从注册表中获取渲染配置
    if (!config)
    {
      console.warn(`No particle config found for type: ${type}`);
      continue;
    }

    const random = createPRNG(seed);
    const [color1, color2] = config.colorRange;
    const [minSize, maxSize] = config.sizeRange;

    for (let x = 0; x < densityGrid.length; x++)
    {
      for (let y = 0; y < (densityGrid[x]?.length ?? 0); y++)
      {
        const count = densityGrid[x][y];
        for (let i = 0; i < count; i++)
        {
          // 粒子生成逻辑保持不变
          const particleX = (x + random()) * TILE_SIZE;
          const particleY = (y + random()) * TILE_SIZE;
          const radius = minSize + (maxSize - minSize) * random();
          const color = random() > 0.5 && color2 ? color2 : color1;

          generatedParticles.push({
            x: particleX,
            y: particleY,
            radius: radius,
            color: color,
          });
        }
      }
    }
  }
  allParticles.value = generatedParticles;
}

onMounted(generateAllParticles);

const shapeConfig = computed(() => ({
  sceneFunc: (context: Context, shape: any) =>
  {
    for (const p of allParticles.value)
    {
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