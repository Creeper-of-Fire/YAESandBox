<!-- src/app-workbench/layouts/EditorLayout.vue -->
<template>
  <div class="editor-layout">
    <!-- 左侧上下结构面板（带分割线） -->
    <n-split  class="left-panels"
              default-size="80%"
              direction="vertical"
    >
      <!-- 1. 结构编辑区 -->
      <template #1>
        <div v-show="isEditorPanelVisible" class="panel editor-panel">
          <slot name="editor-panel"></slot>
        </div>
      </template>

      <!-- 1. 全局资源区 -->
      <template #2>
        <div v-show="isGlobalPanelVisible" class="panel global-panel">
          <slot name="global-panel"></slot>
        </div>
      </template>
    </n-split>

    <!-- 3. 符文编辑区 -->
    <div v-show="isRunePanelVisible" class="panel rune-panel">
      <slot name="rune-panel"></slot>
    </div>

    <!-- 4. 右侧监视器 (暂未实现) -->
    <!--
    <n-layout-sider
      v-if="isMonitorVisible"
      bordered
      :width="350"
    >
      <slot name="monitor-panel"></slot>
    </n-layout-sider>
    -->
  </div>
</template>

<script lang="ts" setup>

const props = defineProps({
  isGlobalPanelVisible: {
    type: Boolean,
    default: true,
  },
  isEditorPanelVisible: {
    type: Boolean,
    default: true,
  },
  isRunePanelVisible: {
    type: Boolean,
    default: true,
  },
  isMonitorVisible: {
    type: Boolean,
    default: false,
  },
});

</script>

<style scoped>
.editor-layout {
  height: 100%;
  overflow: hidden;
  display: flex;
}

/* 左侧上下分割面板容器 */
.left-panels {
  width: 350px;
  height: 100%;
  border-right: 1px solid #e8e8e8; /* 保留原右侧边框 */
}

.panel {
  /* 移除原width固定值，改为撑满父容器 */
  height: 100%;
  flex-shrink: 0;
  box-sizing: border-box;
  transition: width 0.2s ease-in-out, opacity 0.2s ease-in-out;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* 保留原有内边距，移除固定宽度 */
.global-panel {
  padding: 12px 8px;
}

.editor-panel {
  padding: 12px 8px;
}

.rune-panel {
  padding: 12px 8px;
  flex: 1; /* 占据剩余空间 */
  flex-shrink: 0;
}
</style>
<!-- END OF FILE -->