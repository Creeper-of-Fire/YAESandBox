<!-- src/app-workbench/components/.../StepEditor.vue -->
<template>
  <!-- 只有在有上下文的情况下才渲染映射编辑器 -->
  <n-card>
    <template #header>
      <n-flex justify="space-between" align="center">
        <span>编辑步骤：{{ props.stepContext.data.name }}</span>
        <n-form-item label="启用此步骤" label-placement="left" style="margin-bottom: 0;">
          <n-switch v-model:value="props.stepContext.data.enabled" />
        </n-form-item>
      </n-flex>
    </template>
  <StepMappingsEditor
      :available-global-vars="stepContext.availableGlobalVarsForStep"
      :input-mappings="stepContext.data.inputMappings"
      :output-mappings="stepContext.data.outputMappings"
      :required-inputs="requiredStepInputs"
      @update:input-mappings="newMappings => stepContext.data.inputMappings = newMappings"
      @update:output-mappings="newMappings => stepContext.data.outputMappings = newMappings"
  />
  <n-alert
      v-if="!isInWorkflowContext"
      :show-icon="true"
      style="margin: 12px 0;"
      title="独立编辑模式"
      type="warning"
  >
    当前可自由配置输入/输出映射，但完整映射验证和上下文变量建议仅在关联工作流中可用。
    保存前请仔细检查映射配置的正确性。
  </n-alert>

  <StepAiConfigEditor
      v-model="props.stepContext.data.stepAiConfig"
      style="margin-top: 12px; margin-bottom: 12px;"
  />
  </n-card>
</template>

<script lang="ts" setup>
import StepAiConfigEditor from "@/app-workbench/components/step/editor/StepAiConfigEditor.vue";
import {NAlert} from "naive-ui";
import StepMappingsEditor from "@/app-workbench/components/step/editor/StepMappingsEditor.vue";
import {computed, watch, ref} from "vue";
import type {StepEditorContext} from "@/app-workbench/components/step/editor/StepEditorContext.ts";
import { useModuleAnalysisStore } from '@/app-workbench/stores/useModuleAnalysisStore.ts';

const props = defineProps<{
  stepContext: StepEditorContext;
}>();

const moduleAnalysisStore = useModuleAnalysisStore();
const moduleAnalysisResults = ref<Record<string, { consumedVariables: string[], producedVariables: string[] }>>({});

watch(() => props.stepContext.data.modules, async (newModules) => {
  if (newModules) {
    const analysisPromises = newModules.map(async (mod) => {
      // Assuming mod has a unique identifier like configId
      const result = await moduleAnalysisStore.analyzeModule(mod, mod.configId);
      if (result) {
        moduleAnalysisResults.value[mod.configId] = result;
      }
    });
    await Promise.all(analysisPromises);
  }
}, { immediate: true, deep: true });


// 计算属性，判断当前是否处于有上下文的环境中
const isInWorkflowContext = computed(() => props.stepContext.availableGlobalVarsForStep !== undefined);

// 计算属性：计算当前步骤所有模块需要的总输入
const requiredStepInputs = computed(() => {
  const requiredInputs = new Set<string>();
  const producedOutputs = new Set<string>();

  if (props.stepContext.data.modules) {
    for (const mod of props.stepContext.data.modules) {
      const analysisResult = moduleAnalysisResults.value[mod.configId];
      if (analysisResult) {
      (analysisResult.consumedVariables || []).forEach(input => {
        if (!producedOutputs.has(input)) {
          requiredInputs.add(input);
        }
      });
      (analysisResult.producedVariables || []).forEach(output => producedOutputs.add(output));
      }
    }
  }

  return Array.from(requiredInputs);
});

</script>

<style scoped>

</style>