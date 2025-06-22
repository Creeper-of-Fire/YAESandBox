<!-- src/app-workbench/layouts/HeaderAndBodyLayout.vue -->
<template>
  <div class="panel">
    <div class="header">
      <slot name="header"></slot>
    </div>

    <n-scrollbar class="scroll-container" style="height: 100%">
      <slot name="body"></slot>
    </n-scrollbar>
  </div>
</template>


<script lang="ts" setup>
</script>

<style scoped>
.panel {
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
  /* 添加弹性布局 */
  flex: 1;
  overflow: hidden;
}

.scroll-container {
  flex-grow: 1; /* 占据所有剩余空间 */
  overflow: auto; /* 关键！让这个容器自己处理溢出 */
  position: relative; /* 确保内部的滚动条能正确计算高度 */
}
</style>
<!-- END OF FILE -->