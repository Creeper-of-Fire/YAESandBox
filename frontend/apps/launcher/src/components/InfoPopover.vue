<!-- src/components/InfoPopover.vue -->
<script setup lang="ts">
import { ref } from 'vue';

defineProps<{
  text: string | undefined | null;
}>();

const isVisible = ref(false);
const wrapperRef = ref<HTMLDivElement | null>(null);
const popoverStyle = ref({});

const POPOVER_MARGIN = 8;

function showPopover() {
  if (!wrapperRef.value) return;

  // 获取图标相对于视口的位置
  const rect = wrapperRef.value.getBoundingClientRect();

  // 默认定位在图标上方
  let top = rect.top - POPOVER_MARGIN;
  let left = rect.left;

  // 将样式应用到 popover
  // 我们使用 transform 来定位，因为 popover 的高度是动态的
  // 'bottom: 100%' 意味着它的底部与它定位的 top 对齐
  popoverStyle.value = {
    // window.scrollY 确保在页面滚动时位置依然正确
    top: `${top + window.scrollY}px`,
    left: `${left + window.scrollX}px`,
    // 将 popover 向上移动自身的高度，实现顶部对齐
    transform: `translateY(-100%)`,
  };

  isVisible.value = true;
}
function hidePopover() {
  isVisible.value = false;
}
</script>

<template>
  <div
      v-if="text"
      ref="wrapperRef"
      class="info-popover-wrapper"
      @mouseenter="showPopover"
      @mouseleave="hidePopover"
  >
    <!-- SVG Icon -->
    <svg
        class="icon"
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 20 20"
        fill="currentColor"
    >
      <path
          fill-rule="evenodd"
          d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z"
          clip-rule="evenodd"
      />
    </svg>

    <!-- Teleport 将这个 div "传送" 到 .app-shell 标签的末尾 -->
    <Teleport to=".app-shell">
      <Transition name="popover-fade">
        <div v-if="isVisible" class="popover-content" :style="popoverStyle">
          {{ text }}
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.info-popover-wrapper {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: help;
}

.icon {
  width: 1rem;
  height: 1rem;
  color: var(--text-color-muted);
}
</style>

<!--
  这个 style 块是全局的，因为它需要作用于被 Teleport 到 body 的元素。
  这是使用 Teleport 时的常见做法。
-->
<style>
.popover-content {
  position: absolute; /* 相对于视口定位 */
  background-color: var(--bg-color-panel);
  color: var(--text-color-primary);
  border: 1px solid var(--border-color-medium);
  padding: 0.75rem 1rem;
  border-radius: 6px;
  box-shadow: var(--shadow-modal);
  width: max-content;
  max-width: 300px;
  font-size: 0.85rem;
  line-height: 1.5;
  text-align: left;
  z-index: 9999; /* 确保在最顶层 */
  white-space: pre-wrap;
  /* 禁用鼠标事件，防止闪烁 */
  pointer-events: none;
}

/* --- Transition 动画 --- */
.popover-fade-enter-active,
.popover-fade-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.popover-fade-enter-from,
.popover-fade-leave-to {
  opacity: 0;
  transform: translateY(-100%) translateY(5px); /* 初始位置稍微向下偏离 */
}
</style>
