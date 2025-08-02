<!-- src/app-workbench/components/.../TuumEditor.vue -->
<template>
  <!-- 只有在有上下文的情况下才渲染映射编辑器 -->
  <n-card>
    <template #header>
      <n-flex justify="space-between" align="center">
        <span>编辑祝祷：{{ props.tuumContext.data.name }}</span>
        <n-form-item label="启用此祝祷" label-placement="left" style="margin-bottom: 0;">
          <n-switch v-model:value="props.tuumContext.data.enabled" />
        </n-form-item>
      </n-flex>
    </template>
  <TuumMappingsEditor
      :available-global-vars="tuumContext.availableGlobalVarsForTuum"
      :input-mappings="tuumContext.data.inputMappings"
      :output-mappings="tuumContext.data.outputMappings"
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

const props = defineProps<{
  tuumContext: TuumEditorContext;
}>();

const runeAnalysisStore = useRuneAnalysisStore();
const runeAnalysisResults = ref<Record<string, { consumedVariables: string[], producedVariables: string[] }>>({});

watch(() => props.tuumContext.data.runes, async (newRunes) => {
  if (newRunes) {
    const analysisPromises = newRunes.map(async (mod) => {
      // Assuming mod has a unique identifier like configId
      const result = await runeAnalysisStore.analyzeRune(mod, mod.configId);
      if (result) {
        runeAnalysisResults.value[mod.configId] = result;
      }
    });
    await Promise.all(analysisPromises);
  }
}, { immediate: true, deep: true });


// 计算属性，判断当前是否处于有上下文的环境中
const isInWorkflowContext = computed(() => props.tuumContext.availableGlobalVarsForTuum !== undefined);

// 计算属性：计算当前祝祷所有符文需要的总输入
const requiredTuumInputs = computed(() => {
  const requiredInputs = new Set<string>();
  const producedOutputs = new Set<string>();

  if (props.tuumContext.data.runes) {
    for (const mod of props.tuumContext.data.runes) {
      const analysisResult = runeAnalysisResults.value[mod.configId];
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