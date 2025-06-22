<!-- src/app-workbench/components/.../WorkflowItemRenderer.vue -->
<template>
  <div class="workflow-item-renderer">
    <!-- 新增：工作流触发参数编辑器 -->
    <n-card size="small" style="margin-bottom: 16px;" title="工作流触发参数">
      <n-text depth="3" style="font-size: 12px; display: block; margin-bottom: 8px;">
        定义工作流启动时需要从外部传入的参数名称。这些参数后续可以在步骤的输入映射中使用。
      </n-text>
      <!--
        直接将 v-model:value 绑定到 workflow.triggerParams。
        由于 workbenchStore 在 acquireEditSession 时已确保 triggerParams 是一个数组，
        因此这里可以安全地进行绑定。
        NDynamicTags 组件将处理数组的修改（添加、删除标签）。
      -->
      <n-dynamic-tags v-model:value="workflow.triggerParams"/>
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
import {watchEffect} from "vue";

// 定义组件的 Props
const props = defineProps<{
  workflow: WorkflowProcessorConfig;
  session: EditSession;
  selectedModuleId: string | null;
}>();

// 定义组件的 Emits
defineEmits(['update:selectedModuleId']);

// 使用 watchEffect 来确保 props.workflow.triggerParams 是一个数组
watchEffect(() =>
{
  if (props.workflow)
  { // 首先确保 workflow 对象存在
    // 如果 triggerParams 不存在，或者存在但不是一个数组
    if (!props.workflow.triggerParams || !Array.isArray(props.workflow.triggerParams))
    {
      // 直接修改 props.workflow 对象的 triggerParams 属性。
      // 这是安全的，因为 props.workflow 是从 EditSession.getData() 获取的响应式草稿对象。
      // Vue 会侦测到这个修改，并更新相关的依赖。
      props.workflow.triggerParams = [];
    }
  }
});

/**
 * 计算在指定索引的步骤开始执行前，所有可用的全局变量。
 * @param stepIndex - 步骤在工作流中的索引。
 */
function getAvailableVarsForStep(stepIndex: number): string[]
{
  // 由于 watchEffect 的存在，props.workflow.triggerParams 在这里可以安全地假定为一个数组。
  // 如果 props.workflow 本身可能为 null 或 undefined（虽然在这个上下文中不太可能），
  // 则需要添加保护： props.workflow?.triggerParams || []
  const triggerParamsArray = props.workflow.triggerParams || []; // 添加一个防御性检查以防万一
  const availableVars = new Set<string>(triggerParamsArray);

  if (props.workflow && props.workflow.steps)
  { // 确保 steps 存在
    for (let i = 0; i < stepIndex; i++)
    {
      const precedingStep = props.workflow.steps[i];
      if (precedingStep && precedingStep.outputMappings)
      { // 确保 precedingStep 和 outputMappings 存在
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