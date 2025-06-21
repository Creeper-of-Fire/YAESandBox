<!-- src/app-workbench/components/panel/WorkbenchSidebar.vue -->
<template>
  <!-- 1. 父容器需要 position: relative 以便覆盖层定位 -->
  <!-- 2. 监听 dragenter 来激活覆盖层，dragover.prevent 确保 drop 事件可以触发 -->
  <div
      class="workbench-sidebar"
      @dragenter="handleDragEnter"
      @dragover.prevent
  >
    <div v-if="session && session.getData().value">
      <!-- 通用标题区域 -->
      <n-h4 style="display: flex; justify-content: space-between; align-items: center;">
        <span>编辑{{ currentConfigName }}</span>
      </n-h4>

      <!-- 根据会话类型渲染不同的内容 -->
      <template v-if="session.type === 'workflow' && workflowData">
        <p class="sidebar-description">拖拽全局步骤到步骤列表，或将全局资源拖到此区域的任意位置以替换当前编辑项。</p>
        <WorkflowItemRenderer
            :workflow="workflowData"
            :session="session"
            :selected-module-id="selectedModuleId"
            @update:selected-module-id="$emit('update:selectedModuleId', $event)"
        />
      </template>

      <template v-else-if="session.type === 'step' && stepData">
        <p class="sidebar-description">拖拽全局模块到模块列表，或将全局资源拖到此区域的任意位置以替换当前编辑项。</p>
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
        <n-alert title="提示" type="info" style="margin-top: 16px;">
          这是一个独立的模块。请在中间的主编辑区完成详细配置。
        </n-alert>
      </template>
    </div>

    <!-- 3. 拖拽覆盖层 -->
    <div
        v-if="isDragOverContainer"
        class="drop-overlay"
        @dragleave="handleDragLeave"
        @drop.prevent="handleDrop"
        @dragover.prevent
    >
      <div class="drop-overlay-content">
        <n-icon size="48" :component="SwapHorizIcon" />
        <p>释放鼠标以替换当前编辑项</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue';
import {NAlert, NH4, NIcon} from 'naive-ui';
import { SwapHorizOutlined as SwapHorizIcon } from '@vicons/material';
import type {EditSession, ConfigType} from "@/app-workbench/services/EditSession.ts";
import type {
  AbstractModuleConfig,
  StepProcessorConfig,
  WorkflowProcessorConfig
} from "@/app-workbench/types/generated/workflow-config-api-client";
import StepItemRenderer from '../editor/StepItemRenderer.vue';
import WorkflowItemRenderer from "@/app-workbench/components/editor/WorkflowItemRenderer.vue";

const props = defineProps<{
  session: EditSession;
  selectedModuleId: string | null;
}>();

const emit = defineEmits<{
  (e: 'update:selectedModuleId', value: string | null): void;
  (e: 'start-editing', payload: { type: ConfigType; id: string }): void;
}>();

// --- 覆盖层状态 ---
const isDragOverContainer = ref(false);

/**
 * 当拖拽项首次进入容器时，显示覆盖层。
 */
function handleDragEnter(event: DragEvent) {
  // 确保拖拽的数据是我们需要的类型，防止不相关的拖拽（如文件）也触发覆盖层
  if (event.dataTransfer?.types.includes('text/plain')) {
    isDragOverContainer.value = true;
  }
}

/**
 * 当拖拽项离开覆盖层时，隐藏它。
 */
function handleDragLeave() {
  isDragOverContainer.value = false;
}

/**
 * 在覆盖层上完成放置操作。
 */
function handleDrop(event: DragEvent) {
  isDragOverContainer.value = false; // 无论成功与否都隐藏覆盖层
  if (event.dataTransfer) {
    try {
      const dataString = event.dataTransfer.getData('text/plain');
      if (dataString) {
        const { type, id } = JSON.parse(dataString);
        if (type && id) {
          console.log("从全局资源面板拖拽:", type, id);
          // 向上传递事件，请求替换会话
          emit('start-editing', { type, id });
        }
      }
    } catch (e) {
      console.error("解析拖拽数据失败:", e);
    }
  }
}

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
const currentConfigName = computed(() => {
  if (props.session.type === 'workflow' && workflowData.value) return `工作流: ${workflowData.value.name}`;
  if (props.session.type === 'step' && stepData.value) return `步骤: ${stepData.value.name}`;
  if (props.session.type === 'module' && moduleData.value) return `模块: ${moduleData.value.name}`;
  return '未知';
});
</script>

<style scoped>
/* 工作台侧边栏整体样式，必须有 relative 定位 */
.workbench-sidebar {
  position: relative;
  height: 100%;
  box-sizing: border-box;
  overflow-y: auto;
}

.sidebar-description {
  color: #888;
  font-size: 13px;
  margin-top: 8px;
  margin-bottom: 16px;
}

/* 拖拽覆盖层样式 */
.drop-overlay {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-color: rgba(32, 128, 240, 0.15);
  border: 2px dashed #2080f0;
  border-radius: 6px;
  z-index: 10; /* 确保在最上层 */
  display: flex;
  align-items: center;
  justify-content: center;
  box-sizing: border-box;
}

.drop-overlay-content {
  text-align: center;
  color: #2080f0;
  font-weight: 500;
  pointer-events: none; /* 让内容不干扰鼠标事件 */
}
</style>