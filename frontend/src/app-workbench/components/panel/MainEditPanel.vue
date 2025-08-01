<!-- src/app-workbench/components/.../MainEditPanel.vue -->
<template>
  <n-empty v-if="!selectedConfig" description="无激活的编辑会话" style="margin-top: 20%;"/>
  <n-scrollbar>
    <div v-if="selectedType ==='step'" class="main-content-wrapper">
      <StepEditor :step-context="selectedConfig as StepEditorContext"/>
    </div>
    <div v-if="selectedType === 'module'" class="main-content-wrapper">
      <ModuleEditor :module-context="selectedConfig as ModuleEditorContext"/>
    </div>
  </n-scrollbar>
</template>

<script lang="ts" setup>
import ModuleEditor from "@/app-workbench/components/module/editor/ModuleEditor.vue";
import {computed, inject} from "vue";
import {SelectedConfigItemKey} from "@/app-workbench/utils/injectKeys.ts";
import StepEditor from "@/app-workbench/components/step/editor/StepEditor.vue";
import type {StepEditorContext} from "@/app-workbench/components/step/editor/StepEditorContext.ts";
import type {ModuleEditorContext} from "@/app-workbench/components/module/editor/ModuleEditorContext.ts";

const selectedConfigItem = inject(SelectedConfigItemKey);
const selectedConfig = computed(() => selectedConfigItem?.data.value || null);
const selectedType = computed(() =>
{
  const data = selectedConfig.value?.data;
  if (!data) return null;

  // 检查是否包含 AbstractModuleConfig 的特征属性（moduleType）
  if ('moduleType' in data)
  {
    return 'module';  // 模块类型
  }

  return 'step';   // 步骤类型
});

</script>

<style scoped>

</style>