<!-- src/app-workbench/components/share/ConfigItemActionsMenu.vue -->
<!-- TODO 之后把这玩意还加装到ConfigItemBase的右键菜单上 -->
<template>
  <div class="actions-menu-container" @click.stop>
    <!-- 使用 n-dropdown 作为动作菜单的容器 -->
    <n-dropdown
        :options="dropdownOptions"
        placement="bottom-end"
        trigger="hover"
        @select="handleSelect"
    >
      <n-button style="font-size: 20px;" text>
        <n-icon :component="EllipsisHorizontalIcon"/>
      </n-button>
    </n-dropdown>

    <!-- 保留 InlineInputPopover，但它将由 Dropdown 的事件来触发显示 -->
    <InlineInputPopover
        ref="popoverRef"
        :content-type="activePopoverAction?.popoverContentType"
        :default-name-generator="activePopoverAction?.popoverDefaultNameGenerator"
        :initial-value="activePopoverAction?.popoverInitialValue"
        :select-options="activePopoverAction?.popoverSelectOptions"
        :select-placeholder="activePopoverAction?.popoverSelectPlaceholder"
        :title="activePopoverAction?.popoverTitle || ''"
        @confirm="handlePopoverConfirm"
    >
      <!-- 这个插槽内容不会被实际渲染，只是为了让 Popover 有个挂载点 -->
      <div style="display: none;"></div>
    </InlineInputPopover>
  </div>
</template>

<script lang="ts" setup>
import {type Component, computed, h, ref} from 'vue';
import {type DropdownOption, NButton, NDropdown, NIcon, type SelectOption, useDialog, useMessage} from 'naive-ui';
import InlineInputPopover from './InlineInputPopover.vue';
import {EllipsisHorizontalIcon} from '@/utils/icons';

// 接口定义（保持不变，它依然是我们强大的数据模型）
export interface EnhancedAction
{
  key: string;
  label: string;
  icon?: Component;
  disabled?: boolean;
  type?: 'default' | 'primary' | 'info' | 'success' | 'warning' | 'error';
  renderType: 'popover' | 'confirm' | 'button';
  popoverTitle?: string;
  popoverContentType?: 'input' | 'select-and-input';
  popoverSelectOptions?: SelectOption[];
  popoverSelectPlaceholder?: string;
  popoverInitialValue?: string;
  popoverDefaultNameGenerator?: (selectedValue: any, selectOptions: SelectOption[]) => string;
  confirmText?: string;
  handler?: (payload: { name?: string; type?: string }) => void;
}

const props = defineProps<{
  actions: EnhancedAction[];
}>();

const dialog = useDialog();
const popoverRef = ref<InstanceType<typeof InlineInputPopover> | null>(null);
const activePopoverAction = ref<EnhancedAction | null>(null);

// 将我们的 EnhancedAction 数组转换为 Naive UI Dropdown 需要的格式
const dropdownOptions = computed<DropdownOption[]>(() =>
{
  return props.actions.map(action => ({
    label: action.label,
    key: action.key,
    disabled: action.disabled,
    icon: action.icon ? () => h(NIcon, {component: action.icon}) : undefined,
    // 如果有子菜单，可以继续在这里扩展
  }));
});

// 处理 Dropdown 选项的点击事件
function handleSelect(key: string)
{
  const action = props.actions.find(a => a.key === key);
  if (!action) return;

  switch (action.renderType)
  {
    case 'popover':
      // 对于需要弹窗的动作，我们激活 Popover
      activePopoverAction.value = action;
      // 手动触发我们隐藏的 Popover
      popoverRef.value?.handleTriggerClick();
      break;

    case 'confirm':
      // 对于需要确认的动作，弹出确认对话框
      dialog.warning({
        title: '确认操作',
        content: action.confirmText || `确定要执行“${action.label}”吗？`,
        positiveText: '确定',
        negativeText: '取消',
        onPositiveClick: () =>
        {
          action.handler?.({});
        },
      });
      break;

    case 'button':
    default:
      // 对于简单按钮，直接执行
      action.handler?.({});
      break;
  }
}

// 处理 Popover 确认事件
function handlePopoverConfirm(payload: { name: string; type?: string })
{
  if (activePopoverAction.value?.handler)
  {
    activePopoverAction.value.handler(payload);
  }
}
</script>

<style scoped>
.actions-menu-container {
  /* 确保按钮在 item 右侧 */
  display: flex;
  align-items: center;
  padding: 0 8px;
}
</style>