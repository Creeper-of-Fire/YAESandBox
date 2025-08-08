<!-- src/app-workbench/components/.../TuumEditor.vue -->
<template>
  <!-- 只有在有上下文的情况下才渲染映射编辑器 -->
  <n-card>
    <template #header>
      <n-flex align="center" justify="space-between">
        <span>编辑枢机：{{ props.tuumContext.data.name }}</span>
        <n-form-item label="启用此枢机" label-placement="left" style="margin-bottom: 0;">
          <n-switch v-model:value="props.tuumContext.data.enabled"/>
        </n-form-item>
      </n-flex>
    </template>
    <TuumMappingsEditor
        :analysis-result="analysisResult"
        :available-global-vars="tuumContext.availableGlobalVarsForTuum"
        :input-mappings="tuumContext.data.inputMappings"
        :output-mappings="tuumContext.data.outputMappings"
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
import {computed} from "vue";
import type {TuumEditorContext} from "@/app-workbench/components/tuum/editor/TuumEditorContext.ts";
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
</script>

<style scoped>

</style>