<!-- src/app-workbench/components/share/renderer/ConfigItemIconRenderer.vue -->
<template>
  <div class="icon-renderer">
    <!-- 如果有自定义图标 (Emoji 或 SVG) -->
    <template v-if="icon">
      <!-- 主图标区域 -->
      <div class="main-icon-wrapper">
        <div v-if="isSvg" v-html="icon" class="svg-icon-wrapper"></div>
        <span v-else-if="isEmoji" class="emoji-icon">{{ icon }}</span>
      </div>
      <!-- 拖拽功能角标 -->
      <n-icon class="drag-handle-badge" :component="DragHandleIcon" :size="12" />
    </template>

    <!-- 默认/回退方案: 只渲染拖拽图标 -->
    <n-icon v-else :component="defaultIcon" :size="18" />
  </div>
</template>

<script lang="ts" setup>
import { computed, type Component } from 'vue';
import {NIcon, useThemeVars} from 'naive-ui';
import { DragHandleIcon } from '@yaesandbox-frontend/shared-ui/icons';

const props = withDefaults(defineProps<{
  icon?: string | null;
  defaultIcon?: Component;
}>(), {
  icon: null,
  defaultIcon: DragHandleIcon, // 使用原来的拖拽图标作为默认值
});

// 判断是否为 SVG 的简单方法：检查字符串是否以 `<svg` 开头
const isSvg = computed(() => {
  return props.icon?.trim().startsWith('<svg') ?? false;
});

// 判断是否为 Emoji 的简单方法：通过正则表达式匹配
// 这个正则可以匹配大多数现代 Emoji
const isEmoji = computed(() => {
  if (!props.icon || isSvg.value) return false;
  const emojiRegex = /(\p{Emoji_Presentation}|\p{Emoji}\uFE0F)/gu;
  return emojiRegex.test(props.icon);
});

const themeVars = useThemeVars();
const badgeBackgroundColor = computed(() => themeVars.value.actionColor); // 使用一个柔和的背景色
const badgeIconColor = computed(() => themeVars.value.textColor3); // 使用一个不太显眼的文本色
</script>

<style scoped>
/* 根容器使用相对定位，为角标提供定位上下文 */
.icon-renderer {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
}

.main-icon-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
}

.svg-icon-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 20px; /* 主图标稍大一点 */
  height: 20px;
}

.svg-icon-wrapper :deep(svg) {
  width: 100%;
  height: 100%;
  fill: currentColor;
}

.emoji-icon {
  font-size: 18px; /* Emoji 稍大一点 */
  opacity: 0.8;
  line-height: 1;
}

/* 拖拽角标的样式 */
.drag-handle-badge {
  position: absolute;
  right: 0;
  bottom: 0;
  /* 使用父组件传来的颜色，但增加一点不透明度，让它不那么抢眼 */
  opacity: 0.7;
  /* 添加一个小的背景板，让它在复杂的 Emoji 上也能看清 */
  background-color: v-bind(badgeBackgroundColor);
  border-radius: 2px;
  padding: 1px;
  box-sizing: content-box;
  /* 添加一点边框，让它在某些背景色下更清晰 */
  border: 1px solid v-bind('themeVars.borderColor');
}
</style>