<!-- src/components/DownloadProgressBar.vue -->
<template>
  <div class="progress-container">
    <!-- 进度条的宽度绑定到 percentage prop -->
    <div class="progress-bar" :style="{ width: percentage + '%' }"></div>
  </div>
  <!-- 进度文本绑定到 text prop -->
  <p>{{ text }}</p>
</template>

<style scoped>
.progress-container {
  width: 80%;
  max-width: 400px;
  margin: 20px auto;
  background-color: #333;
  border-radius: 5px;
  border: 1px solid #555;
}

.progress-bar {
  height: 20px;
  background-color: #4caf50;
  border-radius: 5px;
  transition: width 0.1s linear;
}

p {
  font-size: 0.9em;
  color: #ccc;
}
</style>

<script setup lang="ts">
// 1. 定义这个组件接收哪些“属性”(props)
defineProps<{
  percentage: number;
  text: string;
}>();

// 2. 这个组件没有自己的逻辑，它纯粹是根据父组件传来的数据展示UI
</script>