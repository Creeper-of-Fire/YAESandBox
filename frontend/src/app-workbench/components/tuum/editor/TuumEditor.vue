<!-- src/app-workbench/components/.../TuumEditor.vue -->
<template>
  <!-- 只有在有上下文的情况下才渲染映射编辑器 -->
  <n-card>
    <template #header>
      <n-flex justify="space-between" align="center">
        <span>编辑枢机：{{ props.tuumContext.data.name }}</span>
        <n-form-item label="启用此枢机" label-placement="left" style="margin-bottom: 0;">
          <n-switch v-model:value="props.tuumContext.data.enabled" />
        </n-form-item>
      </n-flex>
    </template>
  <TuumMappingsEditor
      :available-global-vars="tuumContext.availableGlobalVarsForTuum"
      :input-mappings="tuumContext.data.inputMappings"
      :output-mappings="tuumContext.data.outputMappings"
      :producible-outputs="producibleTuumOutputs"
      :required-inputs="requiredTuumInputs"
      @update:input-mappings="newMappings => tuumContext.data.inputMappings = newMappings"
      @update:output-mappings="newMappings => tuumContext.data.outputMappings = newMappings"
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
  </n-card>
</template>

<script lang="ts" setup>
import {NAlert} from "naive-ui";
import TuumMappingsEditor from "@/app-workbench/components/tuum/editor/TuumMappingsEditor.vue";
import {computed, watch, ref} from "vue";
import type {TuumEditorContext} from "@/app-workbench/components/tuum/editor/TuumEditorContext.ts";
import { useRuneAnalysisStore } from '@/app-workbench/stores/useRuneAnalysisStore.ts';
import {useTuumAnalysis} from "@/app-workbench/composables/useTuumAnalysis.ts";

const props = defineProps<{
  tuumContext: TuumEditorContext;
}>();

// --- 使用新的 useTuumAnalysis Composable ---
const {analysisResult} = useTuumAnalysis(
    // 将整个枢机配置数据作为响应式 Ref 传入
    computed(() => props.tuumContext.data)
);

// 计算属性，判断当前是否处于有上下文的环境中
const isInWorkflowContext = computed(() => props.tuumContext.availableGlobalVarsForTuum !== undefined);

// --- 从 Tuum 分析结果中派生出所需输入 ---
const requiredTuumInputs = computed(() => {
  // 从分析结果的 `consumedEndpoints` 中提取所有需要的外部输入变量名
  return analysisResult.value?.consumedEndpoints
          ?.map(endpoint => endpoint.name)
          // 过滤掉可能为 null 或 undefined 的值
          .filter((name): name is string => !!name)
      ?? [];
});

// --- 从 Tuum 分析结果中派生出可供映射的内部输出 ---
const producibleTuumOutputs = computed(() => {
  // 从分析结果的 `internalVariableDefinitions` 中提取所有内部变量名
  // 这些变量是枢机内所有符文产生的，可以被映射到外部输出
  return Object.keys(analysisResult.value?.internalVariableDefinitions ?? {});
});

</script>

<style scoped>

</style>