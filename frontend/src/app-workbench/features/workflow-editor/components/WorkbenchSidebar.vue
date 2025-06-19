<!-- START OF MODIFIED FILE: src/app-workbench/features/workflow-editor/components/WorkbenchSidebar.vue -->
<template>
  <div class="workbench-sidebar">
    <div v-if="session && session.getData().value">

      <!-- ====================================================== -->
      <!--            Case 1: 正在编辑一个工作流 (Workflow)         -->
      <!-- ====================================================== -->
      <div v-if="session.type === 'workflow' && workflowData">
        <n-h4 style="display: flex; justify-content: space-between; align-items: center;">
          <span>编辑工作流: {{ workflowData.name }}</span>
        </n-h4>

        <draggable
            v-if="workflowData.steps"
            v-model="workflowData.steps"
            item-key="configId"
            group="steps-group"
            handle=".drag-handle"
            class="step-list-container"
            @add="handleAddItem"
        >
          <div v-for="step in workflowData.steps" :key="step.configId" class="step-item">
            <div class="step-header">
              <n-icon class="drag-handle" size="18">
                <DragHandleOutlined/>
              </n-icon>
              <span class="step-name">{{ step.name }}</span>
            </div>
            <draggable
                v-if="step.modules"
                v-model="step.modules"
                item-key="configId"
                group="modules-group"
                handle=".drag-handle"
                class="module-list-container"
                @add="(event) => handleAddItem(event, step.configId)"
            >
              <div
                  v-for="module in step.modules"
                  :key="module.configId"
                  class="module-item"
                  :class="{ 'is-selected': selectedModuleId === module.configId }"
                  @click="selectModule(module.configId)"
              >
                <n-icon class="drag-handle" size="16">
                  <DragHandleOutlined/>
                </n-icon>
                <span class="module-name">{{ module.name }}</span>
              </div>
            </draggable>
          </div>
        </draggable>
      </div>

      <!-- ====================================================== -->
      <!--               Case 2: 正在编辑一个步骤 (Step)            -->
      <!-- ====================================================== -->
      <div v-else-if="session.type === 'step' && stepData">
        <n-h4 style="display: flex; justify-content: space-between; align-items: center;">
          <span>编辑步骤: {{ stepData.name }}</span>
        </n-h4>
        <p class="sidebar-description">您可以重新排序或从左侧拖入新的模块。</p>
        <draggable
            v-if="stepData.modules"
            v-model="stepData.modules"
            item-key="configId"
            group="modules-group"
            handle=".drag-handle"
            class="module-list-container"
            style="margin-top: 16px"
            @add="(event) => handleAddItem(event, stepData?.configId)"
        >
          <div
              v-for="module in stepData.modules"
              :key="module.configId"
              class="module-item"
              :class="{ 'is-selected': selectedModuleId === module.configId }"
              @click="selectModule(module.configId)"
          >
            <n-icon class="drag-handle" size="16">
              <DragHandleOutlined/>
            </n-icon>
            <span class="module-name">{{ module.name }}</span>
          </div>
        </draggable>
      </div>

      <!-- ====================================================== -->
      <!--              Case 3: 正在编辑一个模块 (Module)           -->
      <!-- ====================================================== -->
      <div v-else-if="session.type === 'module' && moduleData">
        <n-h4 style="display: flex; justify-content: space-between; align-items: center;">
          <span>编辑模块: {{ moduleData.name }}</span>
        </n-h4>
        <n-alert title="提示" type="info" style="margin-top: 16px;">
          这是一个独立的模块。请在中间的主编辑区完成详细配置。
        </n-alert>
      </div>

    </div>
  </div>
</template>

<script setup lang="ts">
import {computed} from 'vue';
import {NH4, NIcon} from 'naive-ui';
import {DragHandleOutlined} from '@vicons/material';
import {VueDraggable as draggable} from 'vue-draggable-plus';
import type {EditSession} from "@/app-workbench/features/workflow-editor/services/EditSession.ts";
import type {
  AbstractModuleConfig,
  StepProcessorConfig,
  WorkflowProcessorConfig
} from "@/app-workbench/types/generated/workflow-config-api-client";
// 【修正】从 sortablejs 导入正确的事件类型
import type {SortableEvent} from 'sortablejs';

const props = defineProps<{
  session: EditSession;
}>();


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

const selectedModuleId = computed(() => props.session.getSelectedItemId().value);

function selectModule(moduleId: string) {
  props.session.selectItem(moduleId);
}

/**
 * 处理从全局资源或其他列表【添加】新项的事件。
 * 【核心修正】现在这个函数可以大大简化，因为它只会在合法的拖放发生时被调用。
 * @param {SortableEvent} event
 * @param {string | null} targetStepId 如果为 null，则表示添加到工作流中。 TODO 说实在的这个逻辑挺丑陋的，不过先糊弄着吧。
 */
function handleAddItem(event: SortableEvent, targetStepId: string | null = null) {
  const droppedItemJson = (event.item as HTMLElement).dataset.dragPayload;
  if (!droppedItemJson) return;
  const droppedItem = JSON.parse(droppedItemJson);

  if (event.newIndex === null || event.newIndex === undefined) return;
  const newIndex = event.newIndex;

  // 由于 group 的限制，我们不再需要在这里做复杂的 if/else 判断。
  // 如果 targetStepId 存在，那么拖进来的必然是模块。
  // 如果 targetStepId 不存在，那么拖进来的必然是步骤。
  if (targetStepId) {
    props.session.addModuleToStep(droppedItem as AbstractModuleConfig, targetStepId, newIndex);
  } else {
    props.session.addStep(droppedItem as StepProcessorConfig, newIndex);
  }

  // 成功添加后，移除临时 DOM
  event.item.remove();
}


</script>

<style scoped>
.drag-handle {
  cursor: grab;
  color: #aaa;
  margin-right: 8px;
}

.drag-handle:active {
  cursor: grabbing;
}

.step-list-container {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.step-item {
  background-color: #f7f9fa;
  border: 1px solid #eef2f5;
  border-radius: 6px;
  padding: 8px;
}

.step-header {
  display: flex;
  align-items: center;
  padding: 4px;
  font-weight: 500;
}

.module-list-container {
  min-height: 30px; /* 提供一个可放置的区域 */
  border-radius: 4px;
  margin-top: 8px;
  padding: 4px;
  background-color: #fff;
  border: 1px dashed #dcdfe6;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.module-item {
  display: flex;
  align-items: center;
  padding: 6px 8px;
  background-color: #fff;
  border-radius: 4px;
  border: 1px solid #e0e0e6;
  cursor: pointer;
}

.module-item.is-selected {
  border-color: #2080f0;
  box-shadow: 0 0 0 1px #2080f0;
}

.module-item:hover {
  background-color: #f7f9fa;
}
</style>
<!-- END OF MODIFIED FILE -->