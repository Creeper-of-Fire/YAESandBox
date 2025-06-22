<!-- src/app-workbench/layouts/EditorLayout.vue -->
<template>
  <n-layout class="editor-layout" has-sider>

    <!-- 1. “双子侧边栏”容器 -->
    <n-layout-sider
        :collapsed="sidersCollapsed"
        :collapsed-width="24"
        :width="compositeSiderWidth"
        bordered
        class="composite-sider"
        collapse-mode="width"
        show-trigger="arrow-circle"
        @collapse="sidersCollapsed = true"
        @expand="sidersCollapsed = false"
    >
      <div class="sider-container">
        <!-- 1a. 全局资源面板 (左侧的左栏) -->
        <div v-show="isGlobalPanelVisible" class="panel global-panel">
          <slot name="global-panel"></slot>
        </div>

        <!-- 1b. 当前编辑结构面板 (左侧的右栏) -->
        <div v-show="isEditorPanelVisible" class="panel editor-panel">
          <slot name="editor-panel"></slot>
        </div>
      </div>
    </n-layout-sider>

    <!-- 2. 中间主工作区 -->
    <n-layout-content :native-scrollbar="false" class="main-content">
      <div class="main-content-inner">
        <slot name="main-content"></slot>
      </div>
    </n-layout-content>

    <!-- 3. 右侧监视器 (暂未实现) -->
    <!--
    <n-layout-sider
      v-if="isMonitorVisible"
      bordered
      :width="350"
    >
      <slot name="monitor-panel"></slot>
    </n-layout-sider>
    -->

  </n-layout>
</template>

<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NLayout, NLayoutContent, NLayoutSider} from 'naive-ui';

const props = defineProps({
  isGlobalPanelVisible: {
    type: Boolean,
    default: true,
  },
  isEditorPanelVisible: {
    type: Boolean,
    default: true,
  },
  isMonitorVisible: {
    type: Boolean,
    default: false,
  },
});

// 控制整个复合侧边栏的折叠状态
const sidersCollapsed = ref(false);

// 动态计算“双子侧边栏”的总宽度
const compositeSiderWidth = computed(() =>
{
  if (sidersCollapsed.value) return 24;
  let width = 0;
  if (props.isGlobalPanelVisible) width += 250; // 全局面板宽度
  if (props.isEditorPanelVisible) width += 350; // 编辑面板宽度
  return width;
});
</script>

<style scoped>
.editor-layout {
  height: 100%;
  overflow: hidden;
}

.composite-sider {
  transition: width 0.2s ease-in-out;
  height: 100%;
  overflow: hidden;
  /* 添加弹性布局 */
  display: flex;
  flex-direction: column;
}

.sider-container {
  display: flex;
  flex: 1; /* 关键：占据全部可用空间 */
  height: 100%;
  width: 100%;
  overflow: hidden;
}

.panel {
  height: 100%;
  flex-shrink: 0;
  box-sizing: border-box;
  transition: width 0.2s ease-in-out, opacity 0.2s ease-in-out;
  display: flex;
  flex-direction: column;
  /* 确保面板内部可以滚动 */
  overflow: hidden;
}

/* 确保全局面板高度正确 */
.global-panel {
  width: 250px;
  border-right: 1px solid #e8e8e8;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* 确保编辑面板高度正确 */
.editor-panel {
  width: 350px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
</style>
<!-- END OF FILE -->