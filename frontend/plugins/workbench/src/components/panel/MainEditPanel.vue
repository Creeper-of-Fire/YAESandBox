<!-- src/app-workbench/components/.../MainEditPanel.vue -->
<template>
  <n-empty v-if="!selectedConfig" description="无激活的编辑会话" style="margin-top: 20%;"/>
  <div v-else style="overflow: auto;">
    <n-scrollbar>
      <div v-if="selectedType ==='workflow'" class="main-content-wrapper">
        <WorkflowEditor
            :key="selectedConfig?.data.configId"
            :workflow-context="selectedConfig as WorkflowEditorContext"/>
      </div>
      <div v-if="selectedType ==='tuum'" class="main-content-wrapper">
        <TuumEditor
            :key="selectedConfig?.data.configId"
            :tuum-context="selectedConfig as TuumEditorContext"/>
      </div>
      <div v-if="selectedType === 'rune'" class="main-content-wrapper">
        <RuneEditor
            :key="selectedConfig?.data.configId"
            :rune-context="selectedConfig as RuneEditorContext"/>
      </div>
    </n-scrollbar>
  </div>
</template>

<script lang="ts" setup>
import RuneEditor from "#/components/rune/editor/RuneEditor.vue";
import {computed, inject} from "vue";
import {SelectedConfigItemKey} from "#/utils/injectKeys.ts";
import TuumEditor from "#/components/tuum/editor/TuumEditor.vue";
import type {TuumEditorContext} from "#/components/tuum/editor/TuumEditorContext.ts";
import type {RuneEditorContext} from "#/components/rune/editor/RuneEditorContext.ts";
import WorkflowEditor from "#/components/workflow/editor/WorkflowEditor.vue";
import type {WorkflowEditorContext} from "#/components/workflow/editor/WorkflowEditorContext.ts";

const selectedConfigItem = inject(SelectedConfigItemKey);
const selectedConfig = computed(() =>
    selectedConfigItem?.data.value || null
);
const selectedType = computed(() =>
{
  const data = selectedConfig.value?.data;
  if (!data) return null;

  if ('workflowInputs' in data && 'tuums' in data) {
    return 'workflow';
  }

  // 检查是否包含 AbstractRuneConfig 的特征属性（runeType）
  if ('runeType' in data)
  {
    return 'rune';  // 符文类型
  }

  return 'tuum';   // 枢机类型
});

</script>