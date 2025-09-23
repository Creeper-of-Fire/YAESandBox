<!-- src/components/workflow/editor/InputNode.vue -->
<template>
  <div class="workflow-input-node">
    <div class="node-header">
      <n-icon :component="LoginIcon" :size="16"/>
      <span>工作流输入</span>
    </div>
    <div class="node-body">
      <strong>{{ label }}</strong>
    </div>
    <Handle
        :id="label"
        :position="Position.Right"
        type="source"
    />
  </div>
</template>

<script lang="ts" setup>
import {Handle, Position} from '@vue-flow/core'
import {LoginIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {NIcon} from 'naive-ui';

defineProps<{
  label: string;
}>();
</script>

<style scoped>
.workflow-input-node {
  border: 1px solid #4caf50;
  border-radius: 8px;
  background: #f1f8e9;
  width: 180px;
  font-size: 12px;
}

.node-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px;
  background-color: #dcedc8;
  border-top-left-radius: 7px;
  border-top-right-radius: 7px;
  font-weight: bold;
}

.node-body {
  padding: 12px;
}

/* 隐藏自带的选中边框，我们有自己的高亮逻辑 */
:deep(.vue-flow__node-input.selected) {
  box-shadow: none;
}
</style>