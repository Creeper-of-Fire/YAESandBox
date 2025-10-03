<!-- src/components/SpecialComponentItem.vue -->
<script lang="ts" setup>
import {computed} from 'vue';
import type {Component} from '../stores/updaterStore';
import ComponentItem from './ComponentItem.vue';

const props = defineProps<{
  component: Component,
}>();

// 根据组件状态决定是否应用“可更新”的样式
const isUpdateAvailable = computed(() => props.component.status === 'update_available');
</script>

<template>
  <!--
    这个包装器 div 是关键。
    我们根据状态添加 'update-available' 类，
    然后使用 :deep() 选择器来修改子组件的样式。
  -->
  <div class="special-component-wrapper" :class="{ 'update-available': isUpdateAvailable }">
    <ComponentItem :component="component"/>
  </div>
</template>

<style scoped>
/*
 * 当有可用更新时，给整个包装器添加动画效果
 */
.special-component-wrapper.update-available {
  border-radius: 8px; /* 确保动画的边框是圆角的 */
  border: 2px solid #ffc107;
  box-shadow: 0 4px 15px rgba(255, 193, 7, 0.4);
  animation: pulse-border 2s infinite;
}

/*
 * 使用 :deep() 来修改子组件 ComponentItem 的内部样式
 */
.special-component-wrapper :deep(.component-list-item) {
  /* 移除特殊项的下边框，因为它由外部容器的 border 控制 */
  border-bottom: none;
  padding: 1rem 1.5rem; /* 给特殊项更多内边距 */
  background: linear-gradient(45deg, #fdfcff, #f7f9fc);
}

/*
 * 针对特殊项的酷炫按钮样式，仅在有更新时应用
 */
.special-component-wrapper.update-available :deep(.button-primary) {
  background: linear-gradient(45deg, #ffc107, #ff9800);
  border: none;
  color: #333;
  font-weight: bold;
  transform: scale(1.05); /* 稍微放大 */
  box-shadow: 0 2px 8px rgba(255, 152, 0, 0.5);
  transition: all 0.2s ease;
}

.special-component-wrapper.update-available :deep(.button-primary:hover:not(:disabled)) {
  transform: scale(1.1);
  box-shadow: 0 4px 12px rgba(255, 152, 0, 0.6);
}

/* 定义脉冲动画 */
@keyframes pulse-border {
  0% {
    box-shadow: 0 4px 15px rgba(255, 193, 7, 0.4);
  }
  50% {
    box-shadow: 0 4px 25px rgba(255, 193, 7, 0.7);
  }
  100% {
    box-shadow: 0 4px 15px rgba(255, 193, 7, 0.4);
  }
}
</style>