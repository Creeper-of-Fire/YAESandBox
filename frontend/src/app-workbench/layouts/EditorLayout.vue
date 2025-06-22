<!-- src/app-workbench/layouts/EditorLayout.vue -->
<template>
  <div class="editor-layout" >
    <!--  <n-split :default-size="0.12" :max="0.9" :min="0.1" direction="horizontal">-->

    <!--    <template #1>-->
    <!-- 1. 全局资源区 -->
    <div v-if="isGlobalPanelVisible" class="panel global-panel">
      <slot name="global-panel"></slot>
    </div>
    <!--    </template>-->

    <!--    <template #2>-->
    <!--      <n-split :default-size="0.21" :max="0.9" :min="0.1" direction="horizontal">-->
    <!--        <template #1>-->
    <!-- 2. 结构编辑区 -->
    <div v-if="isEditorPanelVisible" class="panel editor-panel">
      <slot name="editor-panel"></slot>
    </div>

    <!--        </template>-->

    <!--        <template #2>-->
    <!-- 3. 模块编辑区 -->
    <div v-if="isModulePanelVisible" class="panel module-panel">
      <slot name="module-panel"></slot>
    </div>
    <!--        </template>-->


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
    <!--      </n-split>-->
    <!--    </template>-->
    <!--  </n-split>-->

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
  isModulePanelVisible: {
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

.panel {
  height: 100%;
  flex-shrink: 0;
  box-sizing: border-box;
  transition: width 0.2s ease-in-out, opacity 0.2s ease-in-out;
  display: flex;
  flex-direction: column;
  /* 确保面板内部可以滚动 */
  overflow: hidden;
  border-right: 1px solid #e8e8e8;
}

.global-panel {
  padding: 12px 8px;
  width: 250px;
}

.editor-panel {
  padding: 12px 8px;
  width: 350px;
}

.module-panel {
  padding: 12px 8px;
  width: 500px;
}
</style>
<!-- END OF FILE -->