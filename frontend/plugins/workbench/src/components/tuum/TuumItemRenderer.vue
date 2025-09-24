<!-- src/app-workbench/components/.../TuumItemRenderer.vue -->
<template>
  <div class="tuum-item-wrapper">
    <ConfigItemBase
        v-model:enabled="tuum.enabled"
        :highlight-color-calculator="props.tuum.configId || props.tuum.name || 'default-tuum-color'"
        :is-collapsible="isCollapsible"
        :is-draggable="isDraggable"
        :is-selected="isSelected"
        @click="handleItemClick"
        @dblclick="handleDoubleClick"
    >
      <!-- 使用插槽来自定义 Tuum 的标题部分 -->
      <template #content="{ titleClass }">
        <!-- 一个展开/折叠的小箭头 -->
        <n-button v-if="isCollapsible" :focusable="false" text @click.stop="toggleExpansion">
          <template #icon>
            <n-icon :component="isExpanded ? KeyboardArrowUpIcon : KeyboardArrowDownIcon"/>
          </template>
        </n-button>
        <span :class="titleClass" class="tuum-header-content">{{ tuum.name }}</span>
      </template>

      <template #actions>
        <!-- 枢机的操作按钮 -->
        <ConfigItemActionsMenu :actions-provider="getItemActions"/>
      </template>
    </ConfigItemBase>

    <!-- 使用本地的 isExpanded 状态 -->
    <n-collapse-transition :show="isExpanded">
      <div class="rune-list-container">
        <!-- 使用新的可复用列表组件 -->
        <CollapsibleConfigList
            v-model:items="tuum.runes"
            empty-description="拖拽符文到此处"
            group-name="runes-group"
        >
          <!-- 通过作用域插槽定义如何渲染每一项 -->
          <template #item="{ element: runeItem }">
            <RuneItemRenderer
                :parent-tuum="tuum"
                :rune="runeItem"
            />
          </template>
        </CollapsibleConfigList>
      </div>
    </n-collapse-transition>
  </div>
</template>

<script lang="ts" setup>
import {NButton, NCollapseTransition, NIcon, useThemeVars} from 'naive-ui';
import {KeyboardArrowDownIcon, KeyboardArrowUpIcon} from '@yaesandbox-frontend/shared-ui/icons';
import ConfigItemBase from '#/components/share/renderer/ConfigItemBase.vue'; // 导入基础组件
import RuneItemRenderer from '#/components/rune/RuneItemRenderer.vue'; // 导入符文渲染器
import type {TuumConfig, WorkflowConfig} from '#/types/generated/workflow-config-api-client';
import {computed, inject, provide, ref, toRef} from "vue";
import {IsParentDisabledKey} from "#/utils/injectKeys.ts";
import {useConfigItemActions} from "#/composables/useConfigItemActions.ts";
import ConfigItemActionsMenu from "#/components/share/ConfigItemActionsMenu.vue";
import CollapsibleConfigList from "#/components/share/renderer/CollapsibleConfigList.vue";
import {useSelectedConfig} from "#/composables/useSelectedConfig.ts";

// 定义组件的 props
const props = withDefaults(defineProps<{
  tuum: TuumConfig;
  parentWorkflow: WorkflowConfig | null;
  isCollapsible?: boolean; // 是否可折叠
  isDraggable?: boolean;   // 枢机自身是否可拖拽
}>(), {
  isCollapsible: true, // 默认为 true，保持原有行为
  isDraggable: true,   // 默认为 true，保持原有行为
  parentWorkflow: null, // 默认为 null，保持原有行为
});

// UI状态本地化，默认展开
const isExpanded = ref(true);

// Inject
const {selectedConfig,updateSelectedConfig} = useSelectedConfig();

function handleItemClick()
{
  updateSelectedConfig({data: props.tuum});
}

const isSelected = computed(() =>
{
  return selectedConfig?.value?.data.configId === props.tuum.configId;
});

// 使用可组合函数获取动作
const {getActions: getItemActions} = useConfigItemActions({
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

// 提供当前 Tuum 的禁用状态
const isItselfDisabled = computed(() => !props.tuum.enabled);
provide(IsParentDisabledKey, isItselfDisabled);

const themeVars = useThemeVars();
const wrapperBackgroundColor = computed(() => themeVars.value.tableHeaderColor);
const wrapperBorderColor = computed(() => themeVars.value.borderColor);
</script>

<style scoped>
/* 样式保持不变 */
.tuum-item-wrapper {
  background-color: v-bind(wrapperBackgroundColor);
  border-radius: 6px;
  overflow: hidden; /* 确保 ConfigItemBase 的圆角和拖拽柄正确显示 */
}

.tuum-header-content {
  display: flex;
  align-items: center;
}
</style>