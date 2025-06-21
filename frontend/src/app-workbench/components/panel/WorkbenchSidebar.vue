<!-- START OF FILE: src/app-workbench/components/WorkbenchSidebar.vue -->
<template>
  <div class="workbench-sidebar">
    <div v-if="session && session.getData().value">

      <!-- 通用标题区域 -->
      <n-h4 style="display: flex; justify-content: space-between; align-items: center;">
        <span>编辑{{ currentConfigName }}</span>
      </n-h4>

      <!-- 根据会话类型渲染不同的内容 -->
      <template v-if="session.type === 'workflow' && workflowData">
        <p class="sidebar-description">拖拽全局步骤到此处，或在已有的步骤中添加模块。</p>
        <!-- 工作流的步骤列表 (可拖拽排序，接受来自全局资源的步骤) -->
        <draggable
            v-if="workflowData.steps"
            v-model="workflowData.steps"
            item-key="configId"
            group="steps-group"
            handle=".drag-handle"
            class="workflow-step-list-container"
            @add="handleAddStep"
        >
          <!-- 更改类名以区分，避免与 StepItemRenderer 内部的 module-list-container 混淆 -->
          <div v-for="stepItem in workflowData.steps" :key="stepItem.configId" class="step-item">
            <!-- 使用 StepItemRenderer 渲染每个步骤 -->
            <!-- 【核心修正】向下传递 props，向上冒泡 emits -->
            <StepItemRenderer
                :step="stepItem"
                :session="session"
                :selected-module-id="selectedModuleId"
                @update:selected-module-id="$emit('update:selectedModuleId', $event)"
            />
          </div>
        </draggable>
        <!-- 工作流步骤列表为空时的提示 -->
        <n-empty v-else small description="拖拽步骤到此处" class="workflow-step-empty-placeholder"/>

      </template>

      <template v-else-if="session.type === 'step' && stepData">
        <p class="sidebar-description">您可以重新排序或从左侧拖入新的模块。</p>
        <!-- 当直接编辑一个步骤时，只显示其内部的模块列表 -->
        <!-- 注意：这里的模块列表与 StepItemRenderer 内部的模块列表逻辑相似，
                 但因为是顶级编辑，没有 ConfigItemBase 包裹 -->
        <div class="module-list-container" style="margin-top: 16px" :style="{ minHeight: stepData.modules.length > 0 ? 'auto' : '30px' }">
          <draggable
              v-if="stepData.modules"
              v-model="stepData.modules"
              item-key="configId"
              group="modules-group"
              handle=".drag-handle"
              class="module-draggable-area"
              @add="(event) => handleAddModuleToCurrentStep(event, stepData?.configId)"
          >
            <div v-for="moduleItem in stepData.modules" :key="moduleItem.configId" class="module-item">
              <!-- 使用 ModuleItemRenderer 渲染每个模块 -->
              <!-- 【核心修正】向下传递 props，向上冒泡 emits -->
              <ModuleItemRenderer
                  :module="moduleItem"
                  :selected-module-id="selectedModuleId"
                  @update:selected-module-id="$emit('update:selectedModuleId', $event)"
              />
            </div>
          </draggable>
          <n-empty v-else small description="拖拽模块到此处" class="module-empty-placeholder"/>
        </div>
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
import {NAlert, NEmpty, NH4} from 'naive-ui';
import {VueDraggable as draggable} from 'vue-draggable-plus';
import type {EditSession} from "@/app-workbench/services/EditSession.ts";
import type {
  AbstractModuleConfig,
  StepProcessorConfig,
  WorkflowProcessorConfig
} from "@/app-workbench/types/generated/workflow-config-api-client";
import type {SortableEvent} from 'sortablejs';

// 导入新的子组件
import StepItemRenderer from '../editor/StepItemRenderer.vue';
import ModuleItemRenderer from '../editor/ModuleItemRenderer.vue';

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

/**
 * 处理向工作流中【添加】新步骤的事件。
 * 此事件由工作流顶层的 draggable 触发。
 * @param {SortableEvent} event - VueDraggable 的 `add` 事件对象。
 */
function handleAddStep(event: SortableEvent) {
  const droppedItemJson = (event.item as HTMLElement).dataset.dragPayload;
  if (!droppedItemJson) {
    console.warn('拖拽项缺少 data-drag-payload 属性，无法添加步骤。');
    return;
  }
  const droppedItem = JSON.parse(droppedItemJson);

  if (event.newIndex === null || event.newIndex === undefined) {
    console.warn('拖拽事件缺少 newIndex，无法确定步骤插入位置。');
    return;
  }
  const newIndex = event.newIndex;

  props.session.addStep(droppedItem as StepProcessorConfig, newIndex);

  // 成功添加后，移除临时 DOM
  event.item.remove();
}

/**
 * 处理当编辑类型为 'step' 时，向当前步骤中【添加】新模块的事件。
 * @param {SortableEvent} event - VueDraggable 的 `add` 事件对象。
 * @param {string | null | undefined} targetStepId - 目标步骤的 `configId`。
 *                                                   当直接编辑步骤时，这个就是当前 `stepData.configId`。
 */
function handleAddModuleToCurrentStep(event: SortableEvent, targetStepId: string | null | undefined) {
  if (!targetStepId) {
    console.warn('目标步骤ID不存在，无法添加模块。');
    return; // 防御性编程
  }

  const droppedItemJson = (event.item as HTMLElement).dataset.dragPayload;
  if (!droppedItemJson) {
    console.warn('拖拽项缺少 data-drag-payload 属性，无法添加模块。');
    return;
  }
  const droppedItem = JSON.parse(droppedItemJson);

  if (event.newIndex === null || event.newIndex === undefined) {
    console.warn('拖拽事件缺少 newIndex，无法确定模块插入位置。');
    return;
  }
  const newIndex = event.newIndex;

  props.session.addModuleToStep(droppedItem as AbstractModuleConfig, targetStepId, newIndex);

  // 成功添加后，移除临时 DOM
  event.item.remove();
}

// 注意：原有的 `selectModule` 和 `handleAddItem` 方法已被分解并移动到新的组件或由 `EditSession` 直接处理。
</script>

<style scoped>
/* 工作台侧边栏整体样式 */
.workbench-sidebar {
  padding: 12px; /* 内部填充 */
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

/* 工作流中的步骤列表容器样式 */
.workflow-step-list-container {
  display: flex;
  flex-direction: column;
  gap: 16px; /* 步骤之间的间距 */
  min-height: 50px; /* 提供一个可拖拽的最小区域 */
  border: 1px dashed #dcdfe6; /* 虚线边框，表示可拖入 */
  border-radius: 6px;
  padding: 8px;
  margin-top: 16px;
  background-color: #fcfcfc; /* 浅背景色 */
}

/* 工作流步骤列表为空时的占位符样式 */
.workflow-step-empty-placeholder {
  padding: 20px; /* 增加内边距 */
}

/*
   以下是原来在 WorkbenchSidebar 中的样式，它们现在可能被移动到 StepItemRenderer 或 ModuleItemRenderer 内部：
   - .step-item （现在在 StepItemRenderer 内部定义）
   - .step-header （现在由 ConfigItemBase 及其 slot 结构取代）
   - .module-list-container （现在在 StepItemRenderer 内部和 WorkbenchSidebar 编辑 step 类型时定义）
   - .module-item （现在在 ModuleItemRenderer 内部定义）
   - .is-selected （现在在 ConfigItemBase 内部定义）
   - .drag-handle （现在在 ConfigItemBase 内部定义）
*/

/* 仅适用于 WorkbenchSidebar 中编辑 Step 类型时的模块列表样式 */
/* 与 StepItemRenderer 内部的 .module-list-container 结构相同，确保一致性 */
.module-list-container {
  min-height: 30px; /* 提供一个可放置的区域 */
  border-radius: 4px;
  padding: 4px;
  background-color: #fff;
  border: 1px dashed #dcdfe6;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.module-draggable-area {
  min-height: 20px; /* 确保拖拽区域即使没有模块也可见 */
}

.module-empty-placeholder { /* 确保这里也有对应的样式 */
  padding: 10px;
}
</style>
<!-- END OF FILE -->