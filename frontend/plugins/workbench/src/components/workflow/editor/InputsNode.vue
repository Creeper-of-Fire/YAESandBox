<!-- src/components/workflow/editor/InputsNode.vue -->
<template>
  <div class="workflow-inputs-node">
    <div class="node-header">
      <n-icon :component="LoginIcon" :size="16"/>
      <span>工作流输入</span>
    </div>
    <div class="node-body">
      <div v-if="!inputs || !inputs.length" class="endpoint-placeholder">
        无输入
      </div>
      <div v-else class="endpoints-list">
        <div v-for="inputName in inputs" :key="inputName" class="endpoint">
          <span>{{ inputName }}</span>
          <Handle
              :id="inputName"
              :position="Position.Right"
              type="source"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {Handle, type NodeProps, Position} from '@vue-flow/core'
import {LoginIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {NIcon, useThemeVars} from 'naive-ui';
import {computed} from "vue";

// 定义节点 data 对象的类型
interface InputsNodeData {
  inputs: string[];
}

const props = defineProps<NodeProps<InputsNodeData>>();
const inputs = computed(() => props.data.inputs);
const themeVars = useThemeVars();
const headerColor = computed(() => `${themeVars.value.primaryColor}90`);
</script>

<style scoped>
.workflow-inputs-node {
  border: 3px solid v-bind('themeVars.primaryColor');
  border-radius: 8px;
  background: v-bind('themeVars.cardColor');
  min-width: 200px;
  font-size: 12px;
}

.node-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px;
  background-color: v-bind(headerColor);
  border-top-left-radius: 5px;
  border-top-right-radius: 5px;
  font-weight: bold;
}

.node-body {
  padding: 8px;
}

.endpoints-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.endpoint {
  position: relative;
  display: flex;
  justify-content: flex-end;
  align-items: center;
  padding: 4px;
  border-radius: 4px;
  background-color: v-bind('themeVars.bodyColor');
}

.endpoint-placeholder {
  color: v-bind('themeVars.textColor3');
  text-align: center;
  padding: 4px;
  min-height: 30px;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>