<!-- src/app-workbench/components/.../WorkflowItemRenderer.vue -->
<template>
  <div class="workflow-item-renderer">
    <!-- 新增：工作流触发参数编辑器 -->
    <n-card size="small" style="margin-bottom: 16px;" title="工作流触发参数">
      <n-text depth="3" style="font-size: 12px; display: block; margin-bottom: 8px;">
        定义工作流启动时需要从外部传入的参数名称。这些参数后续可以在步骤的输入映射中使用。
      </n-text>
      <n-dynamic-tags v-model:value="triggerParamsRef"/>
    </n-card>


    <!-- 工作流的步骤列表 (可拖拽排序，接受来自全局资源的步骤) -->
    <draggable
        v-if="workflow.steps && workflow.steps.length > 0"
        v-model="workflow.steps"
        :animation="150"
        :group="{ name: 'steps-group', put: ['steps-group'] }"
        class="workflow-step-list-container"
        ghost-class="workbench-ghost-item"
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
import { computed } from 'vue';

// 定义组件的 Props
const props = defineProps<{
  workflow: WorkflowProcessorConfig;
  session: EditSession;
  selectedModuleId: string | null;
}>();

// 定义组件的 Emits
defineEmits(['update:selectedModuleId']);

const triggerParamsRef = computed({
  get: () => props.workflow?.triggerParams || [],
  set: (value) => props.workflow.triggerParams = Array.isArray(value) ? value : []
});

/**
 * 计算在指定索引的步骤开始执行前，所有可用的全局变量。
 * @param stepIndex - 步骤在工作流中的索引。
 */
function getAvailableVarsForStep(stepIndex: number): string[]
{
  const triggerParamsArray = triggerParamsRef.value; // 使用计算属性
  const availableVars = new Set<string>(triggerParamsArray);

  if (props.workflow?.steps) 
  {
    for (let i = 0; i < stepIndex; i++)
    {
      const precedingStep = props.workflow.steps[i];
      if (precedingStep?.outputMappings)
      {
        Object.keys(precedingStep.outputMappings).forEach(globalVar =>
        {
          availableVars.add(globalVar);
        });
      }
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