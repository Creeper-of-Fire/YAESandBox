<!-- src/app-workbench/components/share/ConfigItemActionsMenu.vue -->
<!-- TODO 之后把这玩意还加装到ConfigItemBase的右键菜单上 -->
<template>
  <div v-if="hasActions" class="actions-menu-container" @click.stop>
    <!-- 使用 n-dropdown 作为动作菜单的容器 -->
    <n-dropdown
        :options="dropdownOptions"
        placement="bottom-end"
        trigger="hover"
        @select="handleSelect"
    >
      <n-button ref="triggerButtonRef" style="font-size: 20px;" text>
        <n-icon :component="EllipsisHorizontalIcon"/>
      </n-button>
    </n-dropdown>
  </div>
</template>

<script lang="tsx" setup>
import {computed, ref, toRefs} from 'vue';
import {type DropdownOption, NButton, NDropdown, NIcon} from 'naive-ui';
import {EllipsisHorizontalIcon} from '@yaesandbox-frontend/shared-ui/icons';
import type {EnhancedAction} from "#/components/share/itemActions/useConfigItemActions.tsx";

const props = defineProps<{
  actions: EnhancedAction[];
}>();

const {actions: calculatedActions} = toRefs(props)

const hasActions = computed(() => calculatedActions.value.length > 0);

// 将我们的 EnhancedAction 数组转换为 Naive UI Dropdown 需要的格式
const dropdownOptions = computed<DropdownOption[]>(() =>
{
  return calculatedActions.value.map(action => ({
    label: action.label,
    key: action.key,
    disabled: action.disabled,
    icon: action.icon ? () => <NIcon component={action.icon}/> : undefined,
  }));
});

// 用于获取触发器 DOM 元素的 ref
const triggerButtonRef = ref<InstanceType<typeof NButton> | null>(null);

// 处理 Dropdown 选项的点击事件
function handleSelect(key: string)
{
  const action = calculatedActions.value.find(a => a.key === key);
  const triggerElement = triggerButtonRef.value?.$el as HTMLElement;

  // 如果找不到动作或触发元素，则不执行任何操作
  if (!action || !triggerElement)
  {
    console.error('Action or trigger element not found for key:', key);
    return;
  }

  // 激活！将触发器DOM元素传进去
  action.activate(triggerElement);
}
</script>

<style scoped>
.actions-menu-container {
  /* 确保按钮在 item 右侧 */
  display: flex;
  align-items: center;
}
</style>