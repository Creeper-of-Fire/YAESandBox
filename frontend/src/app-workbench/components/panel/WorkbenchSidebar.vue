<!-- src/app-workbench/components/panel/WorkbenchSidebar.vue -->
<template>
  <!-- 1. 根容器始终存在，并监听拖拽事件 -->
  <div
      class="workbench-sidebar"
      @dragenter="handleDragEnter"
      @dragover.prevent
  >
    <!-- 2. 根据 session 是否存在，渲染不同的内部视图 -->
    <div v-if="session && session.getData().value" class="sidebar-content">
      <!-- 有会话时的视图 -->
      <n-h4 style="display: flex; justify-content: space-between; align-items: center;">
        <span>编辑{{ currentConfigName }}</span>
      </n-h4>

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
    <div v-else class="empty-state-wrapper">
      <!-- 无会话时的空状态视图 -->
      <n-empty description="从左侧拖拽一个配置项到此处开始编辑"/>
    </div>


    <!-- 3. 拖拽覆盖层，覆盖在内容或空状态之上 -->
    <div
        v-if="isDragOverContainer"
        class="drop-overlay"
        @dragleave="handleDragLeave"
        @drop.prevent="handleDrop"
        @dragover.prevent
    >
      <div class="drop-overlay-content">
        <n-icon size="48" :component="SwapHorizIcon"/>
        <p>释放鼠标以{{ session ? '替换当前编辑项' : '开始新的编辑' }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue';
import {NAlert, NEmpty, NH4, NIcon} from 'naive-ui';
import {SwapHorizOutlined as SwapHorizIcon} from '@vicons/material';
import type {ConfigType, EditSession} from "@/app-workbench/services/EditSession.ts";
import type {
  AbstractModuleConfig,
  StepProcessorConfig,
  WorkflowProcessorConfig
} from "@/app-workbench/types/generated/workflow-config-api-client";
import StepItemRenderer from '../editor/StepItemRenderer.vue';
import WorkflowItemRenderer from "@/app-workbench/components/editor/WorkflowItemRenderer.vue";

const props = defineProps<{
  session: EditSession | null;
  selectedModuleId: string | null;
}>();

const emit = defineEmits<{
  (e: 'update:selectedModuleId', value: string | null): void;
  (e: 'start-editing', payload: { type: ConfigType; id: string }): void;
}>();
// --- 覆盖层状态 ---
const isDragOverContainer = ref(false);

// --- 等级定义和比较逻辑 ---

// 定义配置类型的等级
const typeHierarchy: Record<ConfigType, number> = {
  workflow: 3,
  step: 2,
  module: 1,
};

/**
 * 从 DragEvent 中解析出我们自定义的拖拽类型。
 * @param event - 拖拽事件
 * @returns 拖拽的 ConfigType 或 null
 */
function getDraggedItemType(event: DragEvent): ConfigType | null {
  for (const type of event.dataTransfer?.types ?? []) {
    const match = type.match(/^application\/vnd\.workbench\.item\.(workflow|step|module)$/);
    if (match) {
      return match[1] as ConfigType;
    }
  }
  return null;
}


/**
 * 当拖拽项首次进入容器时，进行等级判断。
 */
function handleDragEnter(event: DragEvent) {
  const draggedType = getDraggedItemType(event);

  // 如果无法识别拖拽类型，则不响应该拖拽
  if (!draggedType) {
    return;
  }

  // 如果当前没有编辑会话，任何可识别的拖拽都应该显示覆盖层
  if (!props.session) {
    isDragOverContainer.value = true;
    return;
  }

  // 获取当前会话和拖拽物的等级
  const currentSessionType = props.session.type;
  const draggedLevel = typeHierarchy[draggedType];
  const currentLevel = typeHierarchy[currentSessionType];

  // *** 核心判断逻辑 ***
  // 只有当拖拽项的等级 >= 当前编辑项的等级时，才显示“替换”覆盖层。
  // 否则，不显示覆盖层，让事件“穿透”到下面的 draggable 区域。
  if (draggedLevel >= currentLevel) {
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
  isDragOverContainer.value = false;
  if (event.dataTransfer) {
    try {
      const dataString = event.dataTransfer.getData('text/plain');
      if (dataString) {
        const {type, id} = JSON.parse(dataString);
        if (type && id) {
          emit('start-editing', {type, id});
        }
      }
    } catch (e) {
      console.error("解析拖拽数据失败:", e);
    }
  }
}

// --- 为不同编辑类型创建独立的计算属性，使模板更清晰 ---
const workflowData = computed(() =>
    props.session?.type === 'workflow' ? props.session.getData().value as WorkflowProcessorConfig : null
);
const stepData = computed(() =>
    props.session?.type === 'step' ? props.session.getData().value as StepProcessorConfig : null
);
const moduleData = computed(() =>
    props.session?.type === 'module' ? props.session.getData().value as AbstractModuleConfig : null
);
const currentConfigName = computed(() => {
  if (!props.session) return ''; // 如果没有会话，返回空字符串
  if (props.session.type === 'workflow' && workflowData.value) return `工作流: ${workflowData.value.name}`;
  if (props.session.type === 'step' && stepData.value) return `步骤: ${stepData.value.name}`;
  if (props.session.type === 'module' && moduleData.value) return `模块: ${moduleData.value.name}`;
  return '未知';
});
</script>

<style scoped>
/* 根容器必须是 relative 并且占满高度 */
.workbench-sidebar {
  position: relative;
  height: 100%;
  box-sizing: border-box;
  display: flex; /* 使用 flex 布局让内部内容撑开 */
  flex-direction: column;
}

.sidebar-content {
  overflow-y: auto; /* 内容区域自己滚动 */
  height: 100%;
}

.empty-state-wrapper {
  width: 100%;
  height: 100%;
  display: flex;
  justify-content: center;
  align-items: center;
  border: 2px dashed #dcdfe6;
  border-radius: 8px;
  box-sizing: border-box;
}

.sidebar-description {
  color: #888;
  font-size: 13px;
  margin-top: 8px;
  margin-bottom: 16px;
}

.drop-overlay {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-color: rgba(32, 128, 240, 0.15);
  border: 2px dashed #2080f0;
  border-radius: 6px;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: center;
  box-sizing: border-box;
}

.drop-overlay-content {
  text-align: center;
  color: #2080f0;
  font-weight: 500;
  pointer-events: none;
}
</style>