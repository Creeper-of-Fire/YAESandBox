<!-- launcher/src/App.vue -->
<template>
  <div class="app-shell">
    <div class="app-content">
      <LauncherView/>
    </div>
  </div>
</template>

<script setup lang="ts">
import LauncherView from "./views/LauncherView.vue";
</script>

<style>
/* 样式保持不变 */
.app-shell {
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
}

.app-content {
  flex-grow: 1;
  position: relative;
}
</style>