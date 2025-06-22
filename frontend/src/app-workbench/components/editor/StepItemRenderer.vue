<!-- src/app-workbench/components/.../StepItemRenderer.vue -->
<template>
  <div class="step-item-wrapper">
    <ConfigItemBase
        :is-selected="false"
        is-draggable
        @dblclick="isExpanded = !isExpanded"
    >
      <p class="sidebar-description">您可以重新排序或从左侧拖入新的模块。</p>
      <template #content>
        <span class="step-name">{{ step.name }}</span>
      </template>

      <!-- 双击切换展开/折叠 -->
      <!-- 步骤本身不被“选中”进行配置，而是通过双击展开或点击其内部模块 -->
      <template #actions>
        <!-- 这里可以添加一些步骤特有的操作按钮，例如一个展开/折叠的小箭头 -->
      </template>
      <template #content-below>
        <!-- 使用本地的 isExpanded 状态 -->
        <n-collapse-transition :show="isExpanded">

          <!-- 只有在有上下文的情况下才渲染映射编辑器 -->
          <StepMappingsEditor
              v-if="isInWorkflowContext"
              :available-global-vars="availableGlobalVarsForStep!"
              :input-mappings="step.inputMappings"
              :output-mappings="step.outputMappings"
              :required-inputs="requiredStepInputs"
              @update:input-mappings="newMappings => step.inputMappings = newMappings"
              @update:output-mappings="newMappings => step.outputMappings = newMappings"
          />
          <!-- 如果没有上下文，可以显示一个提示信息 -->
          <n-alert v-else :show-icon="true" style="margin-bottom: 12px;" title="无上下文模式" type="info">
            此步骤正在独立编辑。输入/输出映射的配置和校验仅在工作流编辑器中可用。
          </n-alert>

          <div class="module-list-container">
            <draggable
                v-if="step.modules"
                v-model="step.modules"
                :group="{ name: 'modules-group', put: ['modules-group'] }"
                class="module-draggable-area"
                handle=".drag-handle"
                item-key="configId"
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
            <n-empty v-else description="拖拽模块到此处" small/>
          </div>
        </n-collapse-transition>
      </template>
    </ConfigItemBase>
  </div>
</template>

<script lang="ts" setup>
import {NCollapseTransition, NEmpty} from 'naive-ui';
import {VueDraggable as draggable} from 'vue-draggable-plus';
import ConfigItemBase from './ConfigItemBase.vue'; // 导入基础组件
import ModuleItemRenderer from './ModuleItemRenderer.vue'; // 导入模块渲染器
import type {StepProcessorConfig} from '@/app-workbench/types/generated/workflow-config-api-client';
import type {EditSession} from '@/app-workbench/services/EditSession.ts';
import {computed, ref, watch} from "vue";
import StepMappingsEditor from "@/app-workbench/components/editor/StepMappingsEditor.vue"; // 导入 SortableJS 事件类型

// 定义组件的 props
const props = withDefaults(defineProps<{
  step: StepProcessorConfig;
  session: EditSession;
  selectedModuleId: string | null;
  isCollapsible?: boolean; // 是否可折叠
  isDraggable?: boolean;   // 步骤自身是否可拖拽
  // 从父级(Workflow)传入此步骤可用的全局变量，为空代表不进行检测
  availableGlobalVarsForStep?: string[];
}>(), {
  isCollapsible: true, // 默认为 true，保持原有行为
  isDraggable: true,   // 默认为 true，保持原有行为
});

// UI状态本地化，默认展开
const isExpanded = ref(true);
const emit = defineEmits(['update:selectedModuleId']);


// 计算属性，判断当前是否处于有上下文的环境中
const isInWorkflowContext = computed(() => props.availableGlobalVarsForStep !== undefined);

// 计算属性：计算当前步骤所有模块需要的总输入
const requiredStepInputs = computed(() =>
{
  const inputs = new Set<string>();
  if (props.step.modules)
  {
    for (const mod of props.step.modules)
    {
      mod.consumes.forEach(input => inputs.add(input));
    }
  }
  return Array.from(inputs);
});


// 监听器也需要判断上下文
// TODO 因为循环观测的问题，先删掉
watch(() => props.step.modules, (newModules, oldModules) =>
{
  // 只有在有上下文的情况下，才执行智能协调
  if (isInWorkflowContext.value)
  {
    console.log('在工作流上下文中，模块列表已变化，需要同步输入/输出映射！');
    // TODO: 实现智能协调算法
  }
}, {deep: true});
</script>

<style scoped>
/* 样式保持不变 */
.step-item-wrapper {
  background-color: #f7f9fa; /* 浅灰色背景 */
  border: 1px solid #eef2f5; /* 浅边框 */
  border-radius: 6px;
}

/* 模块列表的容器样式 */
.module-list-container {
  border-radius: 4px;
  margin-top: 8px;
  padding: 8px;
  background-color: #fff;
  border: 1px dashed #dcdfe6; /* 虚线边框，表示可拖入 */
  display: flex;
  flex-direction: column;
  gap: 6px; /* 模块之间的间距 */
}

/* 模块列表为空时的占位符样式 */
.module-empty-placeholder {
  padding: 10px; /* 增加内边距使其更显眼 */
}

/* 模块拖拽区域的最小高度，确保即使没有模块时也能作为拖拽目标 */
.module-draggable-area {
  min-height: 40px;
  display: flex;
  flex-direction: column;
  gap: 6px; /* 确保拖拽项之间也有间距 */
}
</style>