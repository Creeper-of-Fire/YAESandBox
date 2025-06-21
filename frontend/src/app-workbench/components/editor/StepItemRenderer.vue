<!-- src/app-workbench/components/StepItemRenderer.vue -->
<template>
  <div class="step-item-wrapper">
    <ConfigItemBase
        :name="step.name"
        :is-selected="false"
        is-draggable
        @dblclick="isExpanded = !isExpanded"
    >
      <!-- 双击切换展开/折叠 -->
      <!-- 步骤本身不被“选中”进行配置，而是通过双击展开或点击其内部模块 -->
      <template #actions>
        <!-- 这里可以添加一些步骤特有的操作按钮，例如一个展开/折叠的小箭头 -->
      </template>
      <template #content-below>
        <!-- 使用本地的 isExpanded 状态 -->
        <n-collapse-transition :show="isExpanded">
          <div class="module-list-container">
            <draggable
                v-if="step.modules"
                v-model="step.modules"
                item-key="configId"
                group="modules-group"
                handle=".drag-handle"
                class="module-draggable-area"
                @add="(event) => handleAddModule(event)"
            >
              <div v-for="moduleItem in step.modules" :key="moduleItem.configId">
                <!-- 向下传递 props，向上冒泡 emits -->
                <ModuleItemRenderer
                    :module="moduleItem"
                    :selected-module-id="selectedModuleId"
                    @update:selected-module-id="$emit('update:selectedModuleId', $event)"
                />
              </div>
            </draggable>
            <n-empty v-else small description="拖拽模块到此处"/>
          </div>
        </n-collapse-transition>
      </template>
    </ConfigItemBase>
  </div>
</template>

<script setup lang="ts">
import {NCollapseTransition, NEmpty} from 'naive-ui';
import {VueDraggable as draggable} from 'vue-draggable-plus';
import ConfigItemBase from './ConfigItemBase.vue'; // 导入基础组件
import ModuleItemRenderer from './ModuleItemRenderer.vue'; // 导入模块渲染器
import type {AbstractModuleConfig, StepProcessorConfig} from '@/app-workbench/types/generated/workflow-config-api-client';
import type {EditSession} from '@/app-workbench/services/EditSession.ts';
import type {SortableEvent} from 'sortablejs';
import {ref} from "vue"; // 导入 SortableJS 事件类型

// 定义组件的 props
const props = defineProps<{
  step: StepProcessorConfig;
  session: EditSession;
  selectedModuleId: string | null;
}>();

// UI状态本地化，默认展开
const isExpanded = ref(true);
const emit = defineEmits(['update:selectedModuleId']);

/**
 * 处理从全局资源或其他步骤向此步骤中【添加】新模块的事件。
 * @param {SortableEvent} event - VueDraggable 的 `add` 事件对象。
 *                                  当一个元素被拖放到目标列表中时触发。
 */
function handleAddModule(event: SortableEvent) {
  // 从拖拽项的 `dataset.dragPayload` 中获取原始数据
  const droppedItemJson = (event.item as HTMLElement).dataset.dragPayload;
  if (!droppedItemJson) return;
  const droppedItem = JSON.parse(droppedItemJson);
  if (event.newIndex === null || event.newIndex === undefined) return;

  // 使用 props.step.configId
  props.session.addModuleToStep(droppedItem, props.step.configId, event.newIndex);
  event.item.remove();
}
/**
 * 处理模块在【当前步骤内部】排序或【从当前步骤拖出】到其他步骤的事件。
 * VueDraggable 的 `end` 事件在拖拽完成后触发。
 *
 * 对于“在内部排序”：由于 `v-model="step.modules"` 已经双向绑定，VueDraggable 会自动更新 `step.modules` 数组的顺序，
 *                    所以我们不需要在此处额外调用 `session.moveModule`。
 * 对于“从当前步骤拖出到另一个步骤”：VueDraggable 会自动从当前数组中移除该模块，
 *                                  并触发目标步骤的 `add` 事件（由目标步骤的 `StepItemRenderer` 处理 `handleAddModule`）。
 * 因此，此函数主要用于日志记录或其他副作用，实际的业务逻辑已由 `v-model` 和 `handleAddModule` 覆盖。
 * @param {SortableEvent} event - VueDraggable 的 `end` 事件对象。
 */
function handleMoveModule(event: SortableEvent) {
  console.log('模块拖拽结束 (StepItemRenderer 的 end 事件):', event);
  // 在此你可以添加一些日志或当需要处理更复杂的跨组件拖拽同步逻辑时进行操作。
  // 目前的 `addModuleToStep` 和 `v-model` 组合已经满足需求。
}
</script>

<style scoped>
/* 步骤项的容器样式 */
.step-item-wrapper {
  background-color: #f7f9fa; /* 浅灰色背景 */
  border: 1px solid #eef2f5; /* 浅边框 */
  border-radius: 6px;
  padding: 8px;
}

/* 模块列表的容器样式 */
.module-list-container {
  border-radius: 4px;
  margin-top: 8px;
  padding: 4px;
  background-color: #fff;
  border: 1px dashed #dcdfe6; /* 虚线边框，表示可拖入 */
  display: flex;
  flex-direction: column;
  gap: 4px; /* 模块之间的间距 */
}

/* 模块列表为空时的占位符样式 */
.module-empty-placeholder {
  padding: 10px; /* 增加内边距使其更显眼 */
}

/* 模块拖拽区域的最小高度，确保即使没有模块时也能作为拖拽目标 */
.module-draggable-area {
  min-height: 20px;
}
</style>