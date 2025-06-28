<!-- src/app-workbench/components/.../StepEditor.vue -->
<template>
  <!-- 只有在有上下文的情况下才渲染映射编辑器 -->
  <StepMappingsEditor
      v-if="isInWorkflowContext"
      :available-global-vars="stepContext.availableGlobalVarsForStep!"
      :input-mappings="stepContext.data.inputMappings"
      :output-mappings="stepContext.data.outputMappings"
      :required-inputs="requiredStepInputs"
      @update:input-mappings="newMappings => stepContext.data.inputMappings = newMappings"
      @update:output-mappings="newMappings => stepContext.data.outputMappings = newMappings"
  />
  <!-- 如果没有上下文，可以显示一个提示信息 -->
  <n-alert v-else :show-icon="true" style="margin-bottom: 12px;" title="无上下文模式" type="info">
    此步骤正在独立编辑。输入/输出映射的配置和校验仅在工作流编辑器中可用。
  </n-alert>

  <StepAiConfigEditor
      v-model="props.stepContext.data.stepAiConfig"
      style="margin-top: 12px; margin-bottom: 12px;"
  />
</template>

<script lang="ts" setup>
import StepAiConfigEditor from "@/app-workbench/components/editor/StepAiConfigEditor.vue";
import {NAlert} from "naive-ui";
import StepMappingsEditor from "@/app-workbench/components/editor/StepMappingsEditor.vue";
import {computed} from "vue";
import type {StepEditorContext} from "@/app-workbench/components/editor/StepEditorContext.ts";

const props = defineProps<{
  stepContext: StepEditorContext;
}>();


// 计算属性，判断当前是否处于有上下文的环境中
const isInWorkflowContext = computed(() => props.stepContext.availableGlobalVarsForStep !== undefined);

// 计算属性：计算当前步骤所有模块需要的总输入
const requiredStepInputs = computed(() =>
{
  const inputs = new Set<string>();
  if (props.stepContext.data.modules)
  {
    for (const mod of props.stepContext.data.modules)
    {
      // 假设模块的 consumes 存储了其输入变量名
      // @ts-ignore // 如果 consumes 不在标准类型中，可能需要类型断言或更新类型定义
      (mod.consumes || []).forEach(input => inputs.add(input as string));
    }
  }
  return Array.from(inputs);
});

</script>

<style scoped>

</style>