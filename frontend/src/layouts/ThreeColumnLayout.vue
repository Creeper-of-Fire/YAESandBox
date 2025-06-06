﻿<template>
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
      <n-gi
          v-show="!isMobile"
          :span="leftPanelGridSpan"
          class="side-panel-area left-panel-area">
        <div class="panel-wrapper">
          <slot name="left-panel"></slot>
        </div>
      </n-gi>

      <!-- 2. 中间核心内容区 -->
      <n-gi :span="mainContentGridSpan" class="center-content-area">
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
      <n-gi v-show="!isMobile"
            :span="rightPanelGridSpan"
            class="side-panel-area right-panel-area">
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
</template>

<script setup lang="ts">
// =========================================================================================
// COMPONENT: MainLayout.vue
// =========================================================================================
/**
 * @component MainLayout
 * @description
 *   应用的主要布局框架组件。
 *   负责提供响应式的多列布局结构（桌面端三栏，移动端单栏），
 *   并通过具名插槽 (slots) 允许父组件填充工具栏、主内容区域、侧边面板等。
 *   集成了 Naive UI 的全局 Providers (Config, Message, Dialog, Notification)。
 *
 * @usage
 *   <MainLayout>
 *     <template #toolbar>...</template>
 *     <template #left-panel>...</template>
 *     <template #main-content>...</template>
 *     <template #right-panel>...</template>
 *     <template #global-elements>...</template>
 *     <template #drawers>...</template>
 *   </MainLayout>
 *
 * @props
 *   None (当前版本不接收外部 props 来控制布局，内部通过 useMediaQuery 实现响应式)
 *   未来可能添加 props 来控制侧边栏的显隐状态或宽度等。
 *
 * @slots
 *   - `toolbar`: 用于放置应用顶部的工具栏内容。
 *   - `left-panel`: 左侧面板区域，仅在桌面端显示。
 *   - `main-content`: 中间主内容区域。
 *   - `right-panel`: 右侧面板区域，仅在桌面端显示。
 *   - `global-elements`: 用于放置全局覆盖元素，如加载指示器、模态框的容器等。
 *   - `drawers`: 用于放置可能从边缘滑出的抽屉组件。
 *
 * @dependency @vueuse/core (for useMediaQuery)
 * @dependency naive-ui (for NConfigProvider, NLayout, NGrid, etc.)
 *
 * @notes
 *   - 布局响应式基于 Naive UI 的 'm' 断点 (768px)。
 *   - 侧边栏在移动端通过 v-show 隐藏，但其 Grid 项定义仍然存在。
 *   - 样式主要通过 scoped CSS 实现，部分尺寸值定义在 <script setup> 中。
 */
// =========================================================================================

import {computed, ref} from 'vue';
import {
  NLayout, NLayoutHeader, NLayoutContent,
  NGrid, NGi,
} from 'naive-ui';
import {useMediaQuery} from '@vueuse/core';

// Vue 3.3+ defineSlots 宏
// 这个宏是编译时宏，不需要从 'vue' 导入
// 它提供了插槽的类型信息
interface MainLayoutSlots
{
  /**
   * 顶部工具栏 (始终存在)。
   * Toolbar 可以作为 prop 传入或固定在此。
   * 如果 Toolbar 需要与父组件交互，建议还是放在父组件
   * @param props
   */
  toolbar?: (props: {}) => any;
  // 左侧面板插槽 (仅桌面端渲染)
  'left-panel'?: (props: {}) => any;
  // 主要内容插槽
  'main-content'?: (props: {}) => any;
  // 右侧面板插槽 (仅桌面端渲染)
  'right-panel'?: (props: {}) => any;
  // 其他全局元素，例如 Spin 指示器
  'global-elements'?: (props: {}) => any;
  // 如果还需要 Drawer 用于其他功能 (如设置)，可以放在这里
  drawers?: (props: {}) => any;
  // 如果某个插槽是作用域插槽，可以这样定义：
  // 'item-renderer'?: (props: { item: MyItemType, index: number }) => any;
}

defineSlots<MainLayoutSlots>(); // 明确声明插槽


// --- 常量定义 ---
// 使用 CSS 变量来统一定义，便于维护和在 <style> 中使用 v-bind
const SIDE_PANEL_WIDTH_DESKTOP = ref('250px'); // 桌面端侧边空白/抽屉宽度 (使用 ref 以便 v-bind)
const TOOLBAR_HEIGHT = ref('64px');           // 工具栏高度 (使用 ref 以便 v-bind)

// --- 响应式状态 ---
// 根据 Naive UI 的 'm' 断点 (768px)
const isMobile = useMediaQuery('(max-width: 767.9px)');

// --- Computed ---
// Grid 列数：移动端 1 列，桌面端 4 列
const gridCols = computed(() => (isMobile.value ? 1 : 22));

// 各区域的 Grid Span
// 注意：即使在移动端为侧边栏分配 span=1，它们也会因为 v-show="false" 而不显示。
// Naive UI 的 n-gi 在其父 n-grid 的 cols=1 时，span 大于1会被视为1。
const leftPanelGridSpan = computed(() => (isMobile.value ? 1 : 6));
const mainContentGridSpan = computed(() =>
{
  if (isMobile.value)
  {
    return 1; // 移动端，主内容区占满整个单列
  }
  // 桌面端，主内容区的 span 可以根据侧边栏是否显示来动态调整，以填满空间
  // 但为了简单起见并保持 KeepAlive 的稳定性，我们让 ThreeColumnLayout 的结构固定。
  // 如果侧边栏通过 v-show 隐藏，它们在布局上仍占据空间（只是 display:none）。
  // 如果希望主内容区在侧边栏隐藏时扩展，则需要更复杂的 span 计算或不同的 grid 定义。
  // 当前实现：桌面端主内容区固定 span=6。如果侧边栏隐藏，会出现空白区域。
  // 如果需要主内容区扩展，可以调整这里的逻辑，例如：
  // let span = 10;
  // if (props.isLeftPanelActive) span -= 2;
  // if (props.isRightPanelActive) span -= 2;
  // return span;
  // 但要注意，这可能让布局在侧边栏切换时跳动。当前保持固定 span=6。
  return 10;
});
const rightPanelGridSpan = computed(() => (isMobile.value ? 1 : 6));

// 可以选择性地将 isMobile 暴露出去，如果父组件需要的话
// defineExpose({ isMobile });

</script>

<style scoped>
/* 基本样式，确保布局占满屏幕 */
.app-container {
  height: 100vh;
  width: 100vw;
  overflow: hidden;
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