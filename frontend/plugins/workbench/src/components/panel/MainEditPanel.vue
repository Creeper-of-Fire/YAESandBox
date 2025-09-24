<!-- src/app-workbench/components/.../MainEditPanel.vue -->
<template>
  <n-empty v-if="!selectedContext" description="无激活的编辑会话" style="margin-top: 20%;"/>
  <div v-else style="overflow: auto;">
    <n-scrollbar>
      <div v-if="selectedType ==='workflow'" class="main-content-wrapper">
        <WorkflowEditor
            :workflow-context="selectedContext as WorkflowEditorContext"/>
      </div>
      <div v-if="selectedType ==='tuum'" class="main-content-wrapper">
        <TuumEditor
            :tuum-context="selectedContext as TuumEditorContext"/>
      </div>
      <div v-if="selectedType === 'rune'" class="main-content-wrapper">
        <RuneEditor
            :rune-context="selectedContext as RuneEditorContext"/>
      </div>
    </n-scrollbar>
  </div>
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
import {getConfigObjectType} from "#/services/GlobalEditSession";

const {selectedContext} = useSelectedConfig();
const selectedType = computed(() =>
{
  const data = selectedContext.value?.data;
  if (!data) return null;
  const {type} = getConfigObjectType(data)
  return type;
});

</script>