<!-- src/app-workbench/layouts/EditorLayout.vue -->
<template>
  <div class="workbench-main-layout-wrapper">
    <div class="editor-layout-header">
      <div class="header-left-group">
        <slot name="header-left-group"></slot>
        <div v-if="!isMobile">
          <n-switch v-model:value="isGlobalPanelVisible" size="large">
            <template #checked>
              全局资源
            </template>
            <template #unchecked>
              全局资源
            </template>
          </n-switch>

          <n-switch v-model:value="isEditorPanelVisible" size="large">
            <template #checked>
              当前编辑
            </template>
            <template #unchecked>
              当前编辑
            </template>
          </n-switch>
        </div>
      </div>
      <div class="header-right-group">
        <slot name="header-right-group"></slot>
      </div>
    </div>

    <div class="editor-layout">
      <!-- ==================== 1. 宽桌面端布局 (三栏) ==================== -->
      <template v-if="isWideDesktop">
        <div class="wide-desktop-layout">
          <!-- 编辑面板 -->
          <div v-show="isEditorPanelVisible" class="panel editor-panel">
            <slot name="editor-panel"></slot>
          </div>
          <!-- 符文编辑器 -->
          <div class="panel rune-panel">
            <slot name="rune-panel"></slot>
          </div>
          <!-- 全局资源 -->
          <div v-show="isGlobalPanelVisible" class="panel global-panel">
            <slot name="global-panel"></slot>
          </div>
        </div>
      </template>

      <!-- ==================== 2. 窄桌面端布局 (上下分割) ==================== -->
      <template v-else-if="isNarrowDesktop">
        <n-split
            v-show="isEditorPanelVisible&&isGlobalPanelVisible"
            v-model:size="splitSize"
            class="left-panels"
            default-size="80%"
            direction="vertical"
        >
          <template #1>
            <div class="panel editor-panel">
              <slot name="editor-panel"></slot>
            </div>
          </template>
          <template #2>
            <div class="panel global-panel">
              <slot name="global-panel"></slot>
            </div>
          </template>
        </n-split>

        <div v-show="isEditorPanelVisible && !isGlobalPanelVisible" class="left-panels">
          <div class="panel editor-panel">
            <slot name="editor-panel"></slot>
          </div>
        </div>

        <div class="panel rune-panel">
          <slot name="rune-panel"></slot>
        </div>
      </template>

      <!-- ==================== 3. 移动端布局 (抽屉 + Tabs) ==================== -->
      <template v-else>
        <!-- 1. 全局资源区 -> 抽屉 -->
        <n-drawer
            v-model:show="isGlobalPanelDrawerVisible"
            :width="320"
            placement="left"
        >
          <n-drawer-content :closable="true" title="全局资源">
            <div
                class="mobile-drawer-content"
                @drag-from-panel:start="handleDragStartFromPanel"
                @drag-from-panel:end="handleDragEndFromPanel"
            >
              <slot name="global-panel"></slot>
            </div>
          </n-drawer-content>
        </n-drawer>
        <div class="mobile-main-content">
          <!-- 2. 结构区和符文编辑区 -> Tabs -->
          <div class="mobile-tabs-header-wrapper">
            <n-tabs
                v-model:value="activeTab"
                animated
                class="mobile-tabs-bar"
                style="user-select: none"
                type="line"
            >
              <!-- 手动放置 n-tab -->
              <n-tab name="editor">
                结构编辑
              </n-tab>
              <n-tab name="rune">
                符文编辑
              </n-tab>
            </n-tabs>

            <div v-show="activeTab === 'editor'" class="mobile-global-resource-btn">
              <n-button block @click="isGlobalPanelDrawerVisible = true">
                全局资源
              </n-button>
            </div>
          </div>

          <div class="mobile-content-panel">
            <div v-show="activeTab === 'editor'" class="panel editor-panel">
              <slot name="editor-panel"></slot>
            </div>

            <div v-show="activeTab === 'rune'" class="panel rune-panel">
              <slot name="rune-panel"></slot>
            </div>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>
<script lang="ts" setup>

import {NSwitch, useThemeVars} from "naive-ui";
import {breakpointsTailwind, useBreakpoints} from '@vueuse/core';
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import {ref} from "vue";

// --- UI 控制状态 ---
// 使用 vueuse/core 来判断屏幕断点，定义三种布局状态
const breakpoints = useBreakpoints(breakpointsTailwind);
const isMobile = breakpoints.smaller('md'); // < 768px
const isNarrowDesktop = breakpoints.between('md', 'lg'); // 768px ~ 1280px
const isWideDesktop = breakpoints.greaterOrEqual('lg'); // >= 1280px

const isGlobalPanelVisible = useScopedStorage('editor-layout-global-panel-visible', true);
const isEditorPanelVisible = useScopedStorage('editor-layout-editor-panel-visible', true);
const splitSize = useScopedStorage('editor-layout-split-size', '80%');

const isGlobalPanelDrawerVisible = ref(false);

const activeTab = useScopedStorage('editor-active-tab', 'editor');

defineOptions({
  name: 'workbench-main-layout',
})

/**
 * 当监听到从全局资源面板开始拖动的事件时调用
 */
function handleDragStartFromPanel()
{
  // 只在移动端视图下隐藏抽屉
  if (isMobile.value)
  {
    isGlobalPanelVisible.value = false;
  }
}

/**
 * 当监听到拖动结束的事件时调用
 */
function handleDragEndFromPanel()
{
  // 拖动结束后，重新显示抽屉，方便用户继续操作
  // 同样只在移动端生效
  if (isMobile.value)
  {
    isGlobalPanelVisible.value = true;
  }
}

const themeVars = useThemeVars();
</script>

<style scoped>
.workbench-main-layout-wrapper {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.editor-layout {
  flex: 1;
  overflow: hidden;
  display: flex;
}

/* ================= 通用样式 ================= */

.panel {
  height: 100%;
  flex-shrink: 0;
  box-sizing: border-box;
  transition: width 0.2s ease-in-out, opacity 0.2s ease-in-out;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: 12px 8px; /* 统一内边距 */
}

/* ================= 宽桌面端样式 ================= */
.wide-desktop-layout {
  display: flex;
  width: 100%;
  height: 100%;
}

.wide-desktop-layout .global-panel {
  width: 400px;
  border-left: 1px solid v-bind('themeVars.borderColor');
}

.wide-desktop-layout .rune-panel {
  flex: 1; /* 占据中间剩余空间 */
}

.wide-desktop-layout .editor-panel {
  width: 400px;
  border-right: 1px solid v-bind('themeVars.borderColor');
}

/* ================= 窄桌面端样式 ================= */

/* 左侧上下分割面板容器 */
.left-panels {
  width: 400px;
  height: 100%;
  border-right: 1px solid v-bind('themeVars.borderColor');
  /* 当 n-split 隐藏时，平滑过渡 */
  transition: width 0.2s ease-in-out;
}

.left-panels[style*="display: none"] {
  width: 0; /* 配合 transition，实现隐藏动画 */
}

/* 3. 为窄桌面布局下的 rune-panel 添加 flex: 1 */
/* 为了确保样式只在窄桌面下生效，我们不能直接修改 .rune-panel */
/* 而是利用Vue的特性，当 v-else-if="isNarrowDesktop" 渲染时，这两个元素是 .editor-layout 的直接子元素 */
.editor-layout > .rune-panel {
  flex: 1; /* 关键修复：让符文面板占据所有剩余空间 */
}

/* 窄桌面端下的符文面板，占据剩余空间 */
.narrow-desktop-layout .rune-panel {
  flex: 1;
}

/* ================= 移动端样式 ================= */
.mobile-drawer-content {
  padding: 12px 8px;
  height: 100%;
  overflow-y: auto;
  box-sizing: border-box;
}

/* 移动端主内容区的包裹容器 */
.mobile-main-content {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
}

.mobile-tabs-header-wrapper {
  display: flex;
  flex-direction: row;
  align-items: center;
  flex-shrink: 0;
  border-bottom: 1px solid v-bind('themeVars.borderColor');
}

.mobile-tabs-bar {
  flex-grow: 1;
  flex-shrink: 1;
  overflow: hidden;
  padding-left: 16px;
}

/* 用于放置按钮的容器样式 */
.mobile-global-resource-btn {
  display: flex;
  align-items: center;
  padding: 0 16px; /* 给予一些边距 */
}

/* 内容面板的样式 */
.mobile-content-panel {
  flex: 1;
  padding: 12px 8px;
  box-sizing: border-box;
  overflow: auto;
}

/* ================= 头部样式 (通用) ================= */
.editor-layout-header {
  padding: 10px 24px;
  border-bottom: 1px solid v-bind('themeVars.borderColor');
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-shrink: 0;
}

.header-left-group {
  display: flex;
  align-items: center;
  gap: 16px;
}

.header-right-group {
  display: flex;
  gap: 16px;
  align-items: center;
}
</style>
<!-- END OF FILE -->