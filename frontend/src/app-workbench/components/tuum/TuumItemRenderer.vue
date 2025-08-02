<!-- src/app-workbench/components/.../TuumItemRenderer.vue -->
<template>
  <div class="tuum-item-wrapper">
    <ConfigItemBase
        v-model:enabled="tuum.enabled"
        :highlight-color-calculator="props.tuum.configId || props.tuum.name || 'default-tuum-color'"
        :is-collapsible="isCollapsible"
        :is-draggable="isDraggable"
        :is-selected="isSelected"
        @click="updateSelectedConfig"
        @dblclick="handleDoubleClick"
    >
      <!-- 使用插槽来自定义 Tuum 的标题部分 -->
      <template #content>
        <div class="tuum-header-content">
          <span>{{ tuum.name }}</span>
          <!-- 可以考虑在这里添加一个祝祷类型或图标 -->
        </div>
      </template>

      <template #actions>
        <!-- 一个展开/折叠的小箭头 -->
        <n-button v-if="isCollapsible" :focusable="false" text @click.stop="toggleExpansion">
          <template #icon>
            <n-icon :component="isExpanded ? KeyboardArrowUpIcon : KeyboardArrowDownIcon"/>
          </template>
        </n-button>

        <!-- 祝祷的操作按钮 -->
        <ConfigItemActionsMenu :actions="itemActions" />
      </template>
      <template #content-below>
        <!-- 使用本地的 isExpanded 状态 -->
        <n-collapse-transition :show="isExpanded">
          <div class="rune-list-container">
            <draggable
                v-if="tuum.runes"
                v-model="tuum.runes"
                :animation="150"
                :group="{ name: 'runes-group', put: ['runes-group'] }"
                class="rune-draggable-area"
                ghost-class="workbench-ghost-item"
                handle=".drag-handle"
                item-key="configId"
            >
              <div v-for="runeItem in tuum.runes" :key="runeItem.configId" class="rune-item-wrapper">
                <RuneItemRenderer
                    :rune="runeItem"
                    :parent-tuum="tuum"
                />
              </div>
            </draggable>
            <n-empty v-else description="拖拽符文到此处" small/>
          </div>
        </n-collapse-transition>
      </template>
    </ConfigItemBase>
  </div>
</template>

<script lang="ts" setup>
import {NButton, NCollapseTransition, NEmpty, NIcon} from 'naive-ui';
import {EllipsisHorizontalIcon, KeyboardArrowDownIcon, KeyboardArrowUpIcon} from '@/utils/icons.ts';
import {VueDraggable as draggable} from 'vue-draggable-plus';
import ConfigItemBase from '@/app-workbench/components/share/renderer/ConfigItemBase.vue'; // 导入基础组件
import RuneItemRenderer from '@/app-workbench/components/rune/RuneItemRenderer.vue'; // 导入符文渲染器
import type {TuumProcessorConfig, WorkflowProcessorConfig} from '@/app-workbench/types/generated/workflow-config-api-client';
import {computed, inject, ref, toRef} from "vue";
import ColorHash from "color-hash";
import {SelectedConfigItemKey} from "@/app-workbench/utils/injectKeys.ts";
import {useConfigItemActions} from "@/app-workbench/composables/useConfigItemActions.ts";
import ConfigItemActionsMenu from "@/app-workbench/components/share/ConfigItemActionsMenu.vue";

// 定义组件的 props
const props = withDefaults(defineProps<{
  tuum: TuumProcessorConfig;
  parentWorkflow: WorkflowProcessorConfig | null;
  isCollapsible?: boolean; // 是否可折叠
  isDraggable?: boolean;   // 祝祷自身是否可拖拽
  // 从父级(Workflow)传入此祝祷可用的全局变量，为空代表不进行检测
  availableGlobalVarsForTuum?: string[];
}>(), {
  isCollapsible: true, // 默认为 true，保持原有行为
  isDraggable: true,   // 默认为 true，保持原有行为
  parentWorkflow: null, // 默认为 null，保持原有行为
});

// UI状态本地化，默认展开
const isExpanded = ref(true);

// Inject
const selectedConfigItem = inject(SelectedConfigItemKey);

const selectedConfig = selectedConfigItem?.data;

function updateSelectedConfig()
{
  selectedConfigItem?.update({data: props.tuum, availableGlobalVarsForTuum: props.availableGlobalVarsForTuum});
}

const isSelected = computed(() =>
{
  return selectedConfig?.value?.data.configId === props.tuum.configId;
});

// 使用可组合函数获取动作
const {actions: itemActions} = useConfigItemActions({
  itemRef: toRef(props, 'tuum'),
  parentContextRef: computed(() =>
      props.parentWorkflow
          ? {parent: props.parentWorkflow, list: props.parentWorkflow.tuums}
          : null
  ),
});

/**
 * 方法，用于处理 ConfigItemBase 的双击事件。
 * 只有当 isCollapsible 为 true 时，才切换展开状态。
 */
function handleDoubleClick()
{
  if (props.isCollapsible)
  {
    isExpanded.value = !isExpanded.value;
  }
}

/**
 * 方法，用于处理展开/折叠按钮的点击事件。
 * 同样，只有当 isCollapsible 为 true 时才有效。
 * （虽然按钮本身会根据 isCollapsible 来显示/隐藏，但多一层防御是好的）
 */
function toggleExpansion()
{
  if (props.isCollapsible)
  {
    isExpanded.value = !isExpanded.value;
  }
}


// // 监听器也需要判断上下文
// // TODO 因为循环观测的问题，先删掉
// watch(() => props.tuum.runes, (newRunes, oldRunes) =>
// {
//   // 只有在有上下文的情况下，才执行智能协调
//   if (isInWorkflowContext.value)
//   {
//     console.log('在工作流上下文中，符文列表已变化，需要同步输入/输出映射！');
//     // TODO: 实现智能协调算法
//   }
// }, {deep: true});
</script>

<style scoped>
/* 样式保持不变 */
.tuum-item-wrapper {
  background-color: #f7f9fa; /* 浅灰色背景 */
  border: 1px solid #eef2f5; /* 浅边框 */
  border-radius: 6px;
  overflow: hidden; /* 确保 ConfigItemBase 的圆角和拖拽柄正确显示 */
}

.tuum-header-content {
  display: flex;
  align-items: center;
  /* 可以添加更多样式来美化祝祷标题 */
}

/* 符文列表的容器样式 */
.rune-list-container {
  border-radius: 4px;
  margin-top: 8px;
  padding: 8px;
  background-color: #fff;
  border: 1px dashed #dcdfe6; /* 虚线边框，表示可拖入 */
  display: flex;
  flex-direction: column;
  gap: 6px; /* 符文之间的间距 */
}

.rune-item-wrapper {
  /* 可以为每个符文项的包裹 div 添加一些样式，如果需要的话 */
}

/* 符文列表为空时的占位符样式 */
.rune-empty-placeholder {
  padding: 10px; /* 增加内边距使其更显眼 */
}

/* 符文拖拽区域的最小高度，确保即使没有符文时也能作为拖拽目标 */
.rune-draggable-area {
  min-height: 40px;
  display: flex;
  flex-direction: column;
  gap: 6px; /* 确保拖拽项之间也有间距 */
}
</style>