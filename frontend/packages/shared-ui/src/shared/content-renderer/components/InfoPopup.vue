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
import {NPopover, useThemeVars } from 'naive-ui';
// 'text' 属性将由 <info-popup text="..."></info-popup> 注入
defineProps<{
  text: string; // 提示气泡中显示的文本
}>();
const themeVars = useThemeVars(); // 获取主题变量
</script>

<style scoped>
.info-popup-trigger {
  /* 使用主题变量美化样式 */
  text-decoration: underline dotted v-bind('themeVars.primaryColor');
  color: v-bind('themeVars.textColor2');
  padding: 1px 4px;
  border-radius: 4px;
  cursor: help;
  transition: all 0.2s ease;
}

.info-popup-trigger:hover {
  /* 悬浮时，使用主题色作为背景，白色作为文本，形成强烈反差 */
  background-color: v-bind('themeVars.primaryColorHover');
  color: v-bind('themeVars.baseColor'); /* baseColor 通常是白色 */
  text-decoration: none; /* 悬浮时去掉下划线，因为背景色已经足够突出 */
}
</style>