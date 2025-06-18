<!-- START OF FILE: src/app-workbench/features/workflow-editor/components/WorkbenchSidebar.vue -->
<template>
  <div class="workbench-sidebar">
    <div v-if="session">
      <!-- 根据会话类型渲染不同内容 -->
      <div v-if="session.type === 'workflow' && session.getData().value">
        <n-h4>编辑工作流: {{ (session.getData().value as any).name }}</n-h4>
        <!-- TODO: 实现可拖拽的步骤和模块列表 -->
        <p>工作流结构树 (待实现)</p>
      </div>
      <div v-else-if="session.type === 'step'">
        <n-h4>编辑步骤 (待实现)</n-h4>
      </div>
      <div v-else-if="session.type === 'module'">
        <n-h4>编辑模块 (待实现)</n-h4>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {NH4} from 'naive-ui';
import type {EditSession} from "@/app-workbench/features/workflow-editor/services/EditSession.ts";

defineProps<{
  session: EditSession;
}>();
</script>

<style scoped>
/* 样式 */
</style>
<!-- END OF FILE -->