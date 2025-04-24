<template>
  <n-config-provider :theme="lightTheme" class="app-container">
    <n-notification-provider>
      <!-- 最外层容器 -->
      <n-layout style="height: 100vh; width: 100vw; overflow: hidden;">
        <!-- 使用 Grid 布局 -->
        <n-grid
            :x-gap="0"
            :y-gap="0"
            :cols="gridCols" 
        item-responsive
        responsive="screen"
        class="main-layout-grid"
        >
        <!-- 1. 左侧面板插槽 (仅桌面端渲染) -->
        <n-gi v-if="!isMobile" span="2" class="side-panel-area left-panel-area">
          <div class="panel-wrapper">
            <slot name="left-panel"></slot>
          </div>
        </n-gi>

        <!-- 2. 中间核心内容区 -->
        <n-gi span="6" class="center-content-area">
          <div class="center-content-wrapper">
            <!-- 2a. 顶部工具栏 (始终存在) -->
            <n-layout-header bordered class="app-header">
              <!-- Toolbar 可以作为 prop 传入或固定在此 -->
              <!-- 如果 Toolbar 需要与父组件交互，建议还是放在父组件 -->
              <!-- 这里先假设 Toolbar 由父组件通过 Slot 传入或直接放 App.vue -->
              <!-- 为了简化，先放这里 -->
              <slot name="toolbar">
                <!-- 默认 Toolbar 或留空 -->
              </slot>
            </n-layout-header>

            <!-- 2b. 主要内容插槽 -->
            <n-layout-content class="main-content-slot-wrapper">
              <slot name="main-content"></slot>
            </n-layout-content>
          </div>
        </n-gi>

        <!-- 3. 右侧面板插槽 (仅桌面端渲染) -->
        <n-gi v-if="!isMobile" span="2" class="side-panel-area right-panel-area">
          <div class="panel-wrapper">
            <slot name="right-panel"></slot>
          </div>
        </n-gi>
        </n-grid>

        <!-- 其他全局元素，例如 Spin 指示器 -->
        <slot name="global-elements"></slot>

        <!-- 如果还需要 Drawer 用于其他功能 (如设置)，可以放在这里 -->
        <slot name="drawers"></slot>

      </n-layout>
    </n-notification-provider>
  </n-config-provider>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue';
import {
  NConfigProvider, NLayout, NLayoutHeader, NLayoutContent,
  NNotificationProvider, NGrid, NGi,
  lightTheme
} from 'naive-ui';
import { useMediaQuery } from '@vueuse/core';
// --- 常量定义 ---
// 使用 CSS 变量来统一定义，便于维护和在 <style> 中使用 v-bind
const SIDE_PANEL_WIDTH_DESKTOP = ref('250px'); // 桌面端侧边空白/抽屉宽度 (使用 ref 以便 v-bind)
const TOOLBAR_HEIGHT = ref('64px');           // 工具栏高度 (使用 ref 以便 v-bind)

// --- 响应式状态 ---
// 根据 Naive UI 的 'm' 断点 (768px)
const isMobile = useMediaQuery('(max-width: 767.9px)');

// --- Computed ---
// Grid 列数：移动端 1 列，桌面端 4 列
const gridCols = computed(() => (isMobile.value ? 1 : 10));

// 可以选择性地将 isMobile 暴露出去，如果父组件需要的话
// defineExpose({ isMobile });

</script>

<style scoped>
/* 基本样式，确保布局占满屏幕 */
.app-container {
  height: 100vh; width: 100vw; overflow: hidden;
}
.main-layout-grid {
  height: 100%;
  width: 100%;
}

/* Grid 项通用样式 */
.side-panel-area, .center-content-area {
  height: 100%;
  overflow: hidden; /* 防止内部内容溢出 Grid 项 */
  display: flex; /* 使用 Flex 布局 */
  flex-direction: column;
}
.panel-wrapper {
  flex-grow: 1; /* 占据所有可用空间 */
  overflow-y: auto; /* 面板内容可滚动 */
  padding: 10px; /* 给面板内容一些内边距 */
  background-color: #f8f8f9; /* 侧边栏背景色 */
}


/* 中间内容区特定样式 */
.center-content-area {
  background-color: #ffffff; /* 中间背景色 */
}
.center-content-wrapper {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
}
.app-header {
  height: var(64px); /* 使用 CSS 变量或固定值 */
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding: 0 20px; /* Toolbar 内边距 */
}
.main-content-slot-wrapper {
  flex-grow: 1;
  min-height: 0; /* 重要，用于 flex 布局计算 */
  overflow: hidden; /* 由内部内容（如 DynamicScroller）处理滚动 */
  /* background-color: #f0f2f5; */ /* 内容区背景色 */
}

/* 侧边栏区域特定样式 */
.side-panel-area {
  /* 桌面端宽度，可以由 n-grid 的列宽或固定宽度控制 */
  /* 如果用 n-grid 的1fr，则由内容或 min-width 决定 */
  /* 如果需要固定宽度，可能需要放弃 n-grid 而用原生 grid */
  /* 暂时依赖 n-grid 的均分 */
  min-width: 200px; /* 避免侧边栏过窄 */
  border-left: 1px solid #e0e0e6; /* 右侧边栏左边框 */
}
.left-panel-area {
  border-right: 1px solid #e0e0e6; /* 左侧边栏右边框 */
  border-left: none; /* 左侧无边框 */
}
.right-panel-area {
  border-left: 1px solid #e0e0e6; /* 右侧边栏左边框 */
  border-right: none; /* 右侧无边框 */
}

</style>