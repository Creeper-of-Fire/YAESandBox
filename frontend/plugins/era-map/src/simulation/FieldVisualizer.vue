<template>
  <v-layer :opacity="0.5">
    <v-rect
        v-for="cell in cells"
        :key="cell.key"
        :config="cell.config"
    />
  </v-layer>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  grid: number[][];
}>();
const cellSize: number = 20; // 网格单元大小（像素）

// 将网格数据转换为 Konva 可以渲染的单元格配置数组
const cells = computed(() => {
  if (!props.grid || props.grid.length === 0) return [];

  // 找到最大值用于归一化
  let maxPotential = 0;
  for (const row of props.grid) {
    for (const cell of row) {
      if (cell > maxPotential) {
        maxPotential = cell;
      }
    }
  }
  if (maxPotential === 0) maxPotential = 1; // 避免除以零

  const cellConfigs = [];
  for (let i = 0; i < props.grid.length; i++) {
    for (let j = 0; j < props.grid[i].length; j++) {
      const potential = props.grid[i][j];
      const intensity = Math.min(potential / maxPotential, 1); // 归一化强度

      if (intensity > 0.01) { // 只渲染有足够强度的单元格
        cellConfigs.push({
          key: `${i}-${j}`,
          config: {
            x: j * cellSize,
            y: i * cellSize,
            width: cellSize,
            height: cellSize,
            fill: `hsl(${240 - intensity * 240}, 100%, 50%)`, // 从蓝色到红色的渐变
            listening: false, // 不需要监听事件
          },
        });
      }
    }
  }
  return cellConfigs;
});
</script>