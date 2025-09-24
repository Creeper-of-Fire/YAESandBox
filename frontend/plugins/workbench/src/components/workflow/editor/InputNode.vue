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
import {Handle, type NodeProps, Position} from '@vue-flow/core'
import {LoginIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {NIcon, useThemeVars} from 'naive-ui';
import {computed} from "vue";
import {useColorHash} from "#/components/share/renderer/useColorHash.ts";

// 定义节点 data 对象的类型
interface InputNodeData
{
  label: string;
}

// 接收完整的 NodeProps，这才是标准做法
const props = defineProps<NodeProps<InputNodeData>>();

// 从 props.data 中安全地获取 label
const label = computed(() => props.data.label);
const themeVars = useThemeVars();
const headerColor = computed(() => `${themeVars.value.primaryColor}90`)

const {color: highlightColor} = useColorHash(label);

const bodyColor = computed(() => `${highlightColor.value}B0`);
</script>

<style scoped>
.workflow-input-node {
  /* 统一使用主题变量，与 TuumNode 保持一致 */
  border: 3px solid v-bind(bodyColor);
  border-radius: 8px;
  background: v-bind('themeVars.cardColor');
  /* 移除固定宽度，让其自适应内容，但给一个最小宽度以保证美观 */
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
  padding: 12px;
  min-height: 30px; /* 给一个最小高度，避免内容为空时塌陷 */
  display: flex;
  align-items: center;
}
</style>