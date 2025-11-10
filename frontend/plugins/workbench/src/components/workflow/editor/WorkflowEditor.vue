<!-- src/components/workflow/editor/WorkflowEditor.vue -->
<template>
  <div class="workflow-editor-wrapper">
    <!-- 暂时不实现 @nodes-remove="onNodesRemove" -->
    <VueFlow
        :default-viewport="{ zoom: 1 }"
        :edges="edges"
        :max-zoom="4"
        :min-zoom="0.2"
        :multiSelectionKeyCode="'Control'"
        :nodes="nodes"
        class="vue-flow-instance"
        fit-view-on-init
        @connect="handleConnect"
        @edges-change="handleEdgesChange"
        @node-drag-stop="handleNodeDragStop"
    >
      <template #node-workflow-inputs="props">
        <InputsNode v-bind="props"/>
      </template>

      <template #node-tuum="props">
        <TuumNode v-bind="props"/>
      </template>

      <Background/>
      <Controls/>

      <div class="custom-controls-info">
        <n-text depth="3">
          选中连线后，按 <strong>Backspace</strong> 键可删除。
          目前不支持删除节点的逻辑。
        </n-text>
        <n-text depth="3">
          按 <strong>Control</strong> 键可以同时选择多个节点。
        </n-text>
      </div>
    </VueFlow>
  </div>
</template>

<script lang="ts" setup>
import {toRef} from 'vue';
import {VueFlow} from '@vue-flow/core'
import {Background} from '@vue-flow/background';
import {Controls} from '@vue-flow/controls';
import type {WorkflowEditorContext} from "#/components/workflow/editor/WorkflowEditorContext.ts";
import {useWorkflowEditor} from "#/components/workflow/editor/useWorkflowEditor.ts";
import TuumNode from "#/components/workflow/editor/TuumNode.vue";
import InputsNode from "#/components/workflow/editor/InputsNode.vue";
import {useThemeVars} from "naive-ui";

const props = defineProps<{
  workflowContext: WorkflowEditorContext;
}>();

const {
  nodes,
  edges,
  handleConnect,
  handleEdgesChange,
  handleNodeDragStop,
} = useWorkflowEditor(toRef(props, 'workflowContext'));

const themeVars = useThemeVars()
</script>

<style scoped>
.workflow-editor-wrapper {
  width: 100%;
  height: calc(100vh - 180px); /* 减去头部和一些边距的高度 */
  border: 1px solid v-bind('themeVars.borderColor');
  border-radius: 4px;
}

:deep(.vue-flow__node.selected) {
  box-shadow: 0 0 0 2px v-bind('themeVars.primaryColor');
}
</style>