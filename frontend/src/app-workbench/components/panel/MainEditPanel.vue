<!-- src/app-workbench/components/.../MainEditPanel.vue -->
<template>
  <n-empty v-if="!selectedConfig" description="无激活的编辑会话" style="margin-top: 20%;"/>
  <n-scrollbar>
    <div v-if="selectedType ==='step'" class="main-content-wrapper">
      <StepEditor :step-context="selectedConfig as StepEditorContext"/>
    </div>
    <div v-if="selectedType === 'rune'" class="main-content-wrapper">
      <RuneEditor :rune-context="selectedConfig as RuneEditorContext"/>
    </div>
  </n-scrollbar>
</template>

<script lang="ts" setup>
import RuneEditor from "@/app-workbench/components/rune/editor/RuneEditor.vue";
import {computed, inject} from "vue";
import {SelectedConfigItemKey} from "@/app-workbench/utils/injectKeys.ts";
import StepEditor from "@/app-workbench/components/step/editor/StepEditor.vue";
import type {StepEditorContext} from "@/app-workbench/components/step/editor/StepEditorContext.ts";
import type {RuneEditorContext} from "@/app-workbench/components/rune/editor/RuneEditorContext.ts";

const selectedConfigItem = inject(SelectedConfigItemKey);
const selectedConfig = computed(() => selectedConfigItem?.data.value || null);
const selectedType = computed(() =>
{
  const data = selectedConfig.value?.data;
  if (!data) return null;

  // 检查是否包含 AbstractRuneConfig 的特征属性（runeType）
  if ('aiModalType' in data)
  {
    return 'rune';  // 符文类型
  }

  return 'step';   // 步骤类型
});

</script>

<style scoped>

</style>