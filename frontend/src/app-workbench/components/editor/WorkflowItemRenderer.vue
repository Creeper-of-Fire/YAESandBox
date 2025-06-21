<!-- src/app-workbench/components/.../WorkflowItemRenderer.vue -->
<template>
  <div class="workflow-item-renderer">
    <!-- 工作流的步骤列表 (可拖拽排序，接受来自全局资源的步骤) -->
    <draggable
        v-if="workflow.steps && workflow.steps.length > 0"
        v-model="workflow.steps"
        item-key="configId"
        :group="{ name: 'steps-group', put: ['steps-group'] }"
        handle=".drag-handle"
        class="workflow-step-list-container"
        @add="handleAddStep"
    >
      <div v-for="stepItem in workflow.steps" :key="stepItem.configId" class="step-item">
        <!-- 在工作流列表里，使用默认行为的 StepItemRenderer -->
        <div :key="stepItem.configId" class="step-item-container">
          <StepItemRenderer
              :step="stepItem"
              :session="session"
              :selected-module-id="selectedModuleId"
              @update:selected-module-id="$emit('update:selectedModuleId', $event)"
          />
        </div>
      </div>
    </draggable>
    <!-- 工作流步骤列表为空时的提示 -->
    <n-empty v-else small description="拖拽步骤到此处" class="workflow-step-empty-placeholder"/>
  </div>
</template>

<script setup lang="ts">
import {NEmpty} from 'naive-ui';
import {VueDraggable as draggable} from 'vue-draggable-plus';
import type {SortableEvent} from 'sortablejs';
import type {EditSession} from "@/app-workbench/services/EditSession.ts";
import type {WorkflowProcessorConfig} from "@/app-workbench/types/generated/workflow-config-api-client";
import StepItemRenderer from './StepItemRenderer.vue';

// 定义组件的 Props
const props = defineProps<{
  workflow: WorkflowProcessorConfig;
  session: EditSession;
  selectedModuleId: string | null;
}>();

// 定义组件的 Emits
defineEmits(['update:selectedModuleId']);

/**
 * 处理向工作流中【添加】新步骤的事件。
 * 这个函数从 WorkbenchSidebar 完美地移动到了这里。
 * @param {SortableEvent} event - VueDraggable 的 `add` 事件对象。
 */
function handleAddStep(event: SortableEvent) {
  if (event.newIndex === null || event.newIndex === undefined) {
    console.warn('拖拽事件缺少 newIndex，无法处理新步骤。');
    return;
  }
  // 注意：v-model 已经更新了 props.workflow.steps
  const newStep = props.workflow.steps[event.newIndex];
  // 调用 session 来完成对这个新克隆项的初始化
  props.session.initializeClonedItem(newStep);
}
</script>

<style scoped>
/* 工作流中的步骤列表容器样式 */
.workflow-step-list-container {
  display: flex;
  flex-direction: column;
  gap: 16px; /* 步骤之间的间距 */
  min-height: 50px;
  border: 1px dashed #dcdfe6;
  border-radius: 6px;
  padding: 8px;
  background-color: #fcfcfc;
}

/* 单个步骤项在列表中的容器 */
.step-item-container {
  height: 100%;
  /* 未来可以添加样式 */
}

/* 工作流步骤列表为空时的占位符样式 */
.workflow-step-empty-placeholder {
  padding: 20px;
  border: 1px dashed #dcdfe6;
  border-radius: 6px;
  background-color: #fcfcfc;
}
</style>