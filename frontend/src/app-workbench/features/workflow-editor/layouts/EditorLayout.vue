<!-- START OF FILE: src/app-workbench/features/workflow-editor/layouts/EditorLayout.vue -->
<template>
  <n-layout class="editor-layout" has-sider>

    <!-- 1. “双子侧边栏”容器 -->
    <n-layout-sider
        class="composite-sider"
        bordered
        :width="compositeSiderWidth"
        :native-scrollbar="false"
        :collapsed="sidersCollapsed"
        collapse-mode="width"
        :collapsed-width="24"
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
    <n-layout-content class="main-content" :native-scrollbar="false">
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

<script setup lang="ts">
import { computed, ref } from 'vue';
import { NLayout, NLayoutSider, NLayoutContent } from 'naive-ui';

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
const compositeSiderWidth = computed(() => {
  if (sidersCollapsed.value) return 24;
  let width = 0;
  if (props.isGlobalPanelVisible) width += 300; // 全局面板宽度
  if (props.isEditorPanelVisible) width += 300; // 编辑面板宽度
  return width;
});
</script>

<style scoped>
.editor-layout {
  height: 100vh;
  overflow: hidden;
}

.composite-sider {
  transition: width 0.2s ease-in-out;
  height: 100%;
}

.sider-container {
  display: flex;
  height: 100%;
  width: 100%;
  overflow: hidden;
}

.panel {
  height: 100%;
  overflow-y: auto;
  padding: 12px;
  flex-shrink: 0;
  box-sizing: border-box;
  transition: width 0.2s ease-in-out, opacity 0.2s ease-in-out;
}

.global-panel {
  width: 300px;
  border-right: 1px solid #e8e8e8;
}

.editor-panel {
  width: 300px;
}

.main-content {
  background-color: #f7f7f7;
}

.main-content-inner {
  padding: 24px;
  height: 100%;
  box-sizing: border-box;
}
</style>
<!-- END OF FILE -->