<!-- src/app-workbench/components/.../StepItemRenderer.vue -->
<template>
  <div class="step-item-wrapper">
    <ConfigItemBase
        :highlight-color="stepHighlightColor"
        :is-collapsible="isCollapsible"
        :is-draggable="isDraggable"
        :is-selected="false"
        @dblclick="handleDoubleClick"
    >
      <!-- 使用插槽来自定义 Step 的标题部分 -->
      <template #content>
        <div class="step-header-content">
          <span>{{ step.name }}</span>
          <!-- 可以考虑在这里添加一个步骤类型或图标 -->
        </div>
      </template>

      <!-- 双击切换展开/折叠 -->
      <!-- 步骤本身不被“选中”进行配置，而是通过双击展开或点击其内部模块 -->
      <template #actions>
        <!-- 这里可以添加一些步骤特有的操作按钮，例如一个展开/折叠的小箭头 -->
        <n-button v-if="isCollapsible" :focusable="false" text @click.stop="toggleExpansion">
          <template #icon>
            <n-icon :component="isExpanded ? KeyboardArrowUpIcon : KeyboardArrowDownIcon"/>
          </template>
        </n-button>
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

          <StepAiConfigEditor
              v-model="props.step.stepAiConfig"
              style="margin-top: 12px; margin-bottom: 12px;"
          />

          <div class="module-list-container">
            <draggable
                v-if="step.modules"
                v-model="step.modules"
                :animation="150"
                :group="{ name: 'modules-group', put: ['modules-group'] }"
                class="module-draggable-area"
                ghost-class="workbench-ghost-item"
                handle=".drag-handle"
                item-key="configId"
            >
              <div v-for="moduleItem in step.modules" :key="moduleItem.configId" class="module-item-wrapper">
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
import {NAlert, NButton, NCollapseTransition, NEmpty, NIcon} from 'naive-ui';
import {KeyboardArrowDownFilled as KeyboardArrowDownIcon, KeyboardArrowUpFilled as KeyboardArrowUpIcon} from '@vicons/material';
import {VueDraggable as draggable} from 'vue-draggable-plus';
import ConfigItemBase from './ConfigItemBase.vue'; // 导入基础组件
import ModuleItemRenderer from './ModuleItemRenderer.vue'; // 导入模块渲染器
import type {StepProcessorConfig} from '@/app-workbench/types/generated/workflow-config-api-client';
import type {EditSession} from '@/app-workbench/services/EditSession.ts';
import {computed, ref, watch} from "vue";
import StepMappingsEditor from "@/app-workbench/components/editor/StepMappingsEditor.vue"; // 导入 SortableJS 事件类型
import ColorHash from "color-hash";
import StepAiConfigEditor from "@/app-workbench/components/editor/StepAiConfigEditor.vue";

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

const colorHash = new ColorHash({
  lightness: [0.7, 0.75, 0.8],
  saturation: [0.7, 0.8, 0.9],
  hash: 'bkdr'
});

const stepHighlightColor = computed(() =>
{
  // 为步骤也生成一个基于其名称或ID的颜色，使其在工作流列表中也具备视觉区分度
  // 这里使用 configId 确保唯一性，如果希望基于名称，则用 props.step.name
  return colorHash.hex(props.step.configId || props.step.name || 'default-step-color');
});


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
      // 假设模块的 consumes 存储了其输入变量名
      // @ts-ignore // 如果 consumes 不在标准类型中，可能需要类型断言或更新类型定义
      (mod.consumes || []).forEach(input => inputs.add(input as string));
    }
  }
  return Array.from(inputs);
});

/**
 * 方法，用于处理 ConfigItemBase 的双击事件。
 * 只有当 isCollapsible 为 true 时，才切换展开状态。
 */
function handleDoubleClick() {
  if (props.isCollapsible) {
    isExpanded.value = !isExpanded.value;
  }
}

/**
 * 方法，用于处理展开/折叠按钮的点击事件。
 * 同样，只有当 isCollapsible 为 true 时才有效。
 * （虽然按钮本身会根据 isCollapsible 来显示/隐藏，但多一层防御是好的）
 */
function toggleExpansion() {
  if (props.isCollapsible) {
    isExpanded.value = !isExpanded.value;
  }
}


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
  overflow: hidden; /* 确保 ConfigItemBase 的圆角和拖拽柄正确显示 */
}

.step-header-content {
  display: flex;
  align-items: center;
  /* 可以添加更多样式来美化步骤标题 */
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

.module-item-wrapper {
  /* 可以为每个模块项的包裹 div 添加一些样式，如果需要的话 */
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