<template>
  <n-flex class="map-view-container">
    <TavernMap/>
  </n-flex>
</template>

<script lang="ts" setup>
import TavernMap from "#/components/TavernMap.vue";
</script>
<style scoped>
.map-view-container {
  width: 100%;
  height: 100%;
  /* 如果需要，可以添加 flex 属性来控制 TavernMap 的对齐方式 */
  justify-content: center;
  align-items: center;
}
</style>