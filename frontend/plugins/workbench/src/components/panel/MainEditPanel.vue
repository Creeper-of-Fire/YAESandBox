<!-- src/app-workbench/components/.../MainEditPanel.vue -->
<template>
  <div v-if="selectedContext && activeContext" style="overflow: auto;">
    <n-scrollbar>
      <div v-if="selectedType ==='workflow' && workflowEditorContext" class="main-content-wrapper">
        <WorkflowEditor
            :key="`${activeContext.storeId}-${activeContext.version.value}`"
            :workflow-context="workflowEditorContext"/>
      </div>
      <div v-if="selectedType ==='tuum'" class="main-content-wrapper">
        <TuumEditor
            :key="`${activeContext.storeId}-${activeContext.version.value}`"
            :tuum-context="selectedContext as TuumEditorContext"/>
      </div>
      <div v-if="selectedType === 'rune'" class="main-content-wrapper">
        <RuneEditor
            :key="`${activeContext.storeId}-${activeContext.version.value}`"
            :rune-context="selectedContext as RuneEditorContext"/>
      </div>
    </n-scrollbar>
  </div>
  <n-empty v-else description="无激活的编辑会话" style="margin-top: 20%;"/>
</template>

<script lang="ts" setup>
import RuneEditor from "#/components/rune/editor/RuneEditor.vue";
import {computed} from "vue";
import TuumEditor from "#/components/tuum/editor/TuumEditor.vue";
import type {TuumEditorContext} from "#/components/tuum/editor/TuumEditorContext.ts";
import type {RuneEditorContext} from "#/components/rune/editor/RuneEditorContext.ts";
import WorkflowEditor from "#/components/workflow/editor/WorkflowEditor.vue";
import type {WorkflowEditorContext} from "#/components/workflow/editor/WorkflowEditorContext.ts";
import {useSelectedConfig} from "#/services/editor-context/useSelectedConfig.ts";
import {type WorkflowSelectionContext} from "#/services/editor-context/SelectionContext";
import {getConfigObjectType} from "@yaesandbox-frontend/core-services/types";

const {selectedContext, activeContext} = useSelectedConfig();

const workflowEditorContext = computed((): WorkflowEditorContext | null =>
{
  // 确保当前选中的是工作流
  if (selectedContext.value?.type === 'workflow')
  {
    // 从 selectedContext 中安全地提取 storeId 和 data
    const context = selectedContext.value as WorkflowSelectionContext;
    return {
      storeId: context.context.storeId,
      data: context.data
    };
  }
  return null;
});

const selectedType = computed(() =>
{
  const data = selectedContext.value?.data;
  if (!data)
    return null;
  const {type} = getConfigObjectType(data)
  return type;
});

</script>