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
        :input-mappings="inputMappings"
        :output-mappings="outputMappings"
        @update:input-mappings="newMappings => inputMappings = newMappings"
        @update:output-mappings="newMappings => outputMappings = newMappings"
    />
  </n-card>
</template>

<script lang="ts" setup>
import TuumMappingsEditor from "@/components/tuum/editor/TuumMappingsEditor.vue";
import {computed} from "vue";
import type {TuumEditorContext} from "@/components/tuum/editor/TuumEditorContext.ts";
import {useTuumAnalysis} from "@/composables/useTuumAnalysis.ts";

const props = defineProps<{
  tuumContext: TuumEditorContext;
}>();

const inputMappings = computed({
  get()
  {
    return props.tuumContext.data.inputMappingsList
  },
  set(newValue)
  {
    props.tuumContext.data.inputMappingsList = newValue
  }
});
const outputMappings = computed({
  get()
  {
    return props.tuumContext.data.outputMappingsList
  },
  set(newValue)
  {
    props.tuumContext.data.outputMappingsList = newValue
  }
});

// --- 使用新的 useTuumAnalysis Composable ---
const {analysisResult} = useTuumAnalysis(
    // 将整个枢机配置数据作为响应式 Ref 传入
    computed(() => props.tuumContext.data)
);

</script>

<style scoped>

</style>