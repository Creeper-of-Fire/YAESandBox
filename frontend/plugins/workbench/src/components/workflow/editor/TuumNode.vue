<!-- src/components/workflow/editor/TuumNode.vue -->
<template>
  <div :class="{ 'is-disabled': !tuum.enabled }" class="tuum-node">
    <div class="node-header">
      <n-icon :component="TuumIcon" :size="16"/>
      <span>{{ tuum.name }}</span>
    </div>
    <div class="node-body">
      <!-- 输入端点 -->
      <div class="endpoints inputs">
        <div v-for="endpoint in analysisResult?.consumedEndpoints" :key="endpoint.name" class="endpoint">
          <Handle
              :id="endpoint.name"
              :position="Position.Left"
              type="target"
          />
          <VarWithSpecTag :spec-def="endpoint.def" :var-name="endpoint.name"/>
        </div>
        <div v-if="!analysisResult?.consumedEndpoints.length" class="endpoint-placeholder">
          无输入
        </div>
      </div>
      <!-- 输出端点 -->
      <div class="endpoints outputs">
        <div v-for="endpoint in analysisResult?.producedEndpoints" :key="endpoint.name" class="endpoint">
          <VarWithSpecTag :spec-def="endpoint.def" :var-name="endpoint.name"/>
          <Handle
              :id="endpoint.name"
              :position="Position.Right"
              type="source"
          />
        </div>
        <div v-if="!analysisResult?.producedEndpoints.length" class="endpoint-placeholder">
          无输出
        </div>
      </div>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {Handle, Position, type NodeProps} from '@vue-flow/core'
import type {TuumConfig} from "#/types/generated/workflow-config-api-client";
import {computed} from "vue";
import {useTuumAnalysis} from "#/composables/useTuumAnalysis.ts";
import {NIcon, useThemeVars} from "naive-ui";
import {HubIcon as TuumIcon} from '@yaesandbox-frontend/shared-ui/icons'
import VarSpecTag from "#/components/share/varSpec/VarSpecTag.vue";
import VarWithSpecTag from "#/components/share/varSpec/VarWithSpecTag.vue";

interface TuumNodeData {
  tuum: TuumConfig;
}

const props = defineProps<NodeProps<TuumNodeData>>();
const tuum = computed(() => props.data.tuum);
const {analysisResult} = useTuumAnalysis(tuum);

const themeVars = useThemeVars();
</script>

<style scoped>
.tuum-node {
  border: 1px solid v-bind('themeVars.borderColor');
  border-radius: 8px;
  background: v-bind('themeVars.cardColor');
  width: 250px;
  font-size: 12px;
  transition: opacity 0.3s;
}

.tuum-node.is-disabled {
  opacity: 0.5;
}

.node-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px;
  background-color: v-bind('themeVars.actionColor');
  border-top-left-radius: 7px;
  border-top-right-radius: 7px;
  font-weight: bold;
}

.node-body {
  display: flex;
  justify-content: space-between;
  padding: 8px;
}

.endpoints {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 45%;
}

.endpoint {
  position: relative;
  display: flex;
  align-items: center;
  padding: 4px;
  border-radius: 4px;
  background-color: v-bind('themeVars.bodyColor');
}

.inputs .endpoint {
  justify-content: flex-start;
}

.outputs .endpoint {
  justify-content: flex-end;
}

.endpoint-placeholder {
  color: v-bind('themeVars.textColor3');
  text-align: center;
  padding: 4px;
}

:deep(.vue-flow__handle) {
  width: 8px;
  height: 8px;
  background-color: v-bind('themeVars.primaryColor');
}

/* 隐藏自带的选中边框 */
:deep(.vue-flow__node-tuum.selected) {
  box-shadow: 0 0 0 2px v-bind('themeVars.primaryColor');
}
</style>