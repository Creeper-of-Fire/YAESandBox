<!-- components/ThemeTransitionMask.vue -->
<template>
  <div
      v-if="mask.visible"
      class="theme-transition-mask"
      :style="{ backgroundColor: mask.color ,opacity: mask.opacity}"
  ></div>
</template>

<script setup lang="ts">
import { useThemeFallbackMask } from '#/composables/useThemeTransition';

const { fallbackMask: mask } = useThemeFallbackMask();
</script>

<style scoped>
.theme-transition-mask {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  width: 100vw;
  height: 100vh;
  z-index: 99999; /* 确保在最顶层 */
  pointer-events: none; /* 允许点击穿透 */
}
</style>