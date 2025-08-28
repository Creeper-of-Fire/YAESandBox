<!-- InfoPopup.vue -->
<template>
  <n-popover trigger="hover">
    <!-- 触发器：slot中的内容，即<info-popup>包裹的文本 -->
    <template #trigger>
      <span class="info-popup-trigger" style="text-decoration: underline dotted; cursor: help;">
        <slot></slot>
      </span>
    </template>

    <!-- 提示内容：由'text'属性提供 -->
    {{ text }}
  </n-popover>
</template>
<script lang="ts" setup>

// 'text' 属性将由 <info-popup text="..."></info-popup> 注入
defineProps<{
  text: string; // 提示气泡中显示的文本
}>();
</script>