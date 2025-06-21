<!-- src/app-workbench/components/.../WorkbenchSidebar.vue -->
<template>
  <div class="workbench-sidebar">
    <div v-if="session && session.getData().value">

      <!-- 通用标题区域 -->
      <n-h4 style="display: flex; justify-content: space-between; align-items: center;">
        <span>编辑{{ currentConfigName }}</span>
      </n-h4>

      <!-- 根据会话类型渲染不同的内容 -->
      <!-- 渲染工作流 -->
      <template v-if="session.type === 'workflow' && workflowData">
        <p class="sidebar-description">拖拽全局步骤到此处，或在已有的步骤中添加模块。</p>
        <WorkflowItemRenderer
            :workflow="workflowData"
            :session="session"
            :selected-module-id="selectedModuleId"
            @update:selected-module-id="$emit('update:selectedModuleId', $event)"
        />
      </template>

      <template v-else-if="session.type === 'step' && stepData">
        <p class="sidebar-description">您可以重新排序或从左侧拖入新的模块。</p>
        <!--
          复用 StepItemRenderer 来显示模块列表。
          - is-collapsible=false: 始终展开，不可折叠。
          - is-draggable=false: 步骤本身不可拖动，因为它就是当前编辑的主体。
        -->
        <StepItemRenderer
            :step="stepData"
            :session="session"
            :selected-module-id="selectedModuleId"
            @update:selected-module-id="$emit('update:selectedModuleId', $event)"
            :is-collapsible="false"
            :is-draggable="false"
            style="margin-top: 16px"
        />
      </template>

      <template v-else-if="session.type === 'module' && moduleData">
        <!-- 当直接编辑一个模块时，显示提示信息 -->
        <n-alert title="提示" type="info" style="margin-top: 16px;">
          这是一个独立的模块。请在中间的主编辑区完成详细配置。
        </n-alert>
      </template>

    </div>
  </div>
</template>

<script setup lang="ts">
import {computed} from 'vue';
import {NAlert, NH4} from 'naive-ui';
import type {EditSession} from "@/app-workbench/services/EditSession.ts";
import type {
  AbstractModuleConfig,
  StepProcessorConfig,
  WorkflowProcessorConfig
} from "@/app-workbench/types/generated/workflow-config-api-client";

// 导入新的子组件
import StepItemRenderer from '../editor/StepItemRenderer.vue';
import WorkflowItemRenderer from "@/app-workbench/components/editor/WorkflowItemRenderer.vue";

const props = defineProps<{
  session: EditSession;
  selectedModuleId: string | null;
}>();

defineEmits(['update:selectedModuleId']);


// --- 为不同编辑类型创建独立的计算属性，使模板更清晰 ---
const workflowData = computed(() =>
    props.session.type === 'workflow' ? props.session.getData().value as WorkflowProcessorConfig : null
);
const stepData = computed(() =>
    props.session.type === 'step' ? props.session.getData().value as StepProcessorConfig : null
);
const moduleData = computed(() =>
    props.session.type === 'module' ? props.session.getData().value as AbstractModuleConfig : null
);

// 计算当前编辑项的显示名称
const currentConfigName = computed(() => {
  if (props.session.type === 'workflow' && workflowData.value) return `工作流: ${workflowData.value.name}`;
  if (props.session.type === 'step' && stepData.value) return `步骤: ${stepData.value.name}`;
  if (props.session.type === 'module' && moduleData.value) return `模块: ${moduleData.value.name}`;
  return '未知'; // 默认值
});
</script>

<style scoped>
/* 工作台侧边栏整体样式 */
.workbench-sidebar {
  height: 100%; /* 占满父容器高度 */
  box-sizing: border-box; /* 边框和填充包含在宽度内 */
  overflow-y: auto; /* 内容溢出时显示滚动条 */
}

/* 侧边栏描述文本样式 */
.sidebar-description {
  color: #888;
  font-size: 13px;
  margin-top: 8px;
  margin-bottom: 16px;
}
</style>
<!-- END OF FILE -->