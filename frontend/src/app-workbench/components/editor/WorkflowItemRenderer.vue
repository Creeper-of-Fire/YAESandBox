<!-- src/app-workbench/components/.../WorkflowItemRenderer.vue -->
<template>
  <div class="workflow-item-renderer">
    <!-- 工作流的步骤列表 (可拖拽排序，接受来自全局资源的步骤) -->
    <draggable
        v-if="workflow.steps && workflow.steps.length > 0"
        v-model="workflow.steps"
        :group="{ name: 'steps-group', put: ['steps-group'] }"
        class="workflow-step-list-container"
        handle=".drag-handle"
        item-key="configId"
    >
      <div v-for="(stepItem, index) in workflow.steps" :key="stepItem.configId" class="step-item">
        <!-- 在工作流列表里，使用默认行为的 StepItemRenderer -->
        <div :key="stepItem.configId" class="step-item-container">
          <StepItemRenderer
              :available-global-vars-for-step="getAvailableVarsForStep(index)"
              :selected-module-id="selectedModuleId"
              :session="session"
              :step="stepItem"
              @update:selected-module-id="$emit('update:selectedModuleId', $event)"
          />
        </div>
      </div>
    </draggable>
    <!-- 工作流步骤列表为空时的提示 -->
    <n-empty v-else class="workflow-step-empty-placeholder" description="拖拽步骤到此处" small/>
  </div>
</template>

<script lang="ts" setup>
import {NEmpty} from 'naive-ui';
import {VueDraggable as draggable} from 'vue-draggable-plus';
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
 * 计算在指定索引的步骤开始执行前，所有可用的全局变量。
 * @param stepIndex - 步骤在工作流中的索引。
 */
function getAvailableVarsForStep(stepIndex: number): string[]
{
  const availableVars = new Set<string>(props.workflow.triggerParams || []);

  for (let i = 0; i < stepIndex; i++)
  {
    const precedingStep = props.workflow.steps[i];
    if (precedingStep.outputMappings)
    {
      // outputMappings 的 key 是全局变量名
      Object.keys(precedingStep.outputMappings).forEach(globalVar =>
      {
        availableVars.add(globalVar);
      });
    }
  }

  return Array.from(availableVars);
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