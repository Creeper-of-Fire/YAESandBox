<!-- src/app-workbench/components/share/InlineInputPopover.vue -->
<template>
  <n-popover
      v-model:show="isPopoverVisible"
      :on-clickoutside="handleClickOutside"
      :style="{ width: '300px' }"
      placement="right"
      trigger="manual"
  >
    <template #trigger>
      <div @click="handleTriggerClick">
        <slot></slot>
      </div>
    </template>

    <!-- Popover 内部的内容 -->
    <n-flex vertical v-if="action">
      <n-h5 style="margin: 0 0 8px 0;">{{ action.popoverTitle }}</n-h5>

      <!-- 不同的内容类型渲染 -->
      <template v-if="contentType === 'input' || contentType === 'select-and-input'">
        <!-- 符文类型选择 (仅在 content-type 为 'select-and-input' 时显示) -->
        <n-form-item v-if="contentType === 'select-and-input'" label="类型" required>
          <n-select
              v-model:value="selectValue"
              :options="action.popoverSelectOptions"
              :placeholder="action.popoverSelectPlaceholder"
              filterable
          />
        </n-form-item>

        <!-- 名称输入 -->
        <n-form-item v-if="showNameInput" label="名称" required>
          <n-input
              ref="inputRef"
              v-model:value="inputValue"
              :placeholder="action.popoverInitialValue"
              @keydown.enter.prevent="handlePositiveClick"
          />
        </n-form-item>
      </template>

      <!-- 确认删除模式 -->
      <template v-else-if="contentType === 'confirm-delete'">
        <n-alert :show-icon="false" style="margin-bottom: 8px;" type="warning">
          {{ action.popoverConfirmMessage || '确定要执行此操作吗？' }}
        </n-alert>
      </template>

      <!-- 操作按钮 -->
      <n-flex justify="end">
        <n-button size="small" @click="handleNegativeClick">{{ negativeText }}</n-button>
        <n-button :disabled="!isInputValid" size="small" type="primary" @click="handlePositiveClick">
          {{ positiveText }}
        </n-button>
      </n-flex>
    </n-flex>
  </n-popover>
</template>

<script lang="ts" setup>
import {computed, nextTick, ref, watch} from 'vue';
import type {InputInst} from 'naive-ui';
import {NAlert, NButton, NFlex, NFormItem, NH5, NInput, NPopover, NSelect} from 'naive-ui';
import type {EnhancedAction} from "#/composables/useConfigItemActions.ts";

const props = withDefaults(defineProps<{
      action?: EnhancedAction;
      positiveText?: string;
      negativeText?: string;
    }>()
    , {
      action: () => ({
        key: 'blank',
        label: 'blank',
        renderType: 'button',
        disabled: true,
      }),
      positiveText: '确认',
      negativeText: '取消',
    }
);


const emit = defineEmits<{
  (e: 'confirm', payload: { name?: string; type?: string }): void;
}>();

// --- 内部状态 ---
const isPopoverVisible = ref(false);
const inputRef = ref<InputInst | null>(null);
const selectValue = ref<string | null>(null);
const inputValue = ref<string>('');

// --- 计算属性 ---
const contentType = computed(() => props.action.popoverContentType);
const selectOptions = computed(() => props.action.popoverSelectOptions || []);

const showNameInput = computed(() =>
{
  // 只有在 input 或 select-and-input 模式下才显示名称输入
  return contentType.value === 'input' || (contentType.value === 'select-and-input' && !!selectValue.value);
});

// onMounted(() =>
// {
//   // 初始化输入框的值
//   if (props.action.popoverDefaultNameGenerator)
//   {
//     inputValue.value = ''
//   }
//   else if (props.action.popoverInitialValue)
//   {
//     inputValue.value = props.action.popoverInitialValue;
//     console.log('initialValue', props.action.popoverInitialValue);
//   }
// })

const isInputValid = computed(() =>
{
  // 确认删除模式下，直接认为有效（只需要点击确认）
  if (contentType.value === 'confirm-delete') return true;

  // 对于输入模式，检查输入框是否有效
  if (contentType.value === 'input') return inputValue.value.trim() !== '';

  // 对于选择+输入模式，检查选择和输入是否都有效
  if (contentType.value === 'select-and-input')
  {
    return !!selectValue.value && inputValue.value.trim() !== '';
  }

  return false; // 其他未知情况
});

// --- 监视器 ---
watch(selectValue, (newType) =>
{
  if (newType && contentType.value === 'select-and-input')
  {
    if (props.action.popoverDefaultNameGenerator)
    {
      inputValue.value = props.action.popoverDefaultNameGenerator(newType, selectOptions.value);
    }
    else
    {
      const option = selectOptions.value.find(opt => opt.value === newType);
      inputValue.value = option?.label as string || '新项目';
    }
  }
  else if (contentType.value !== 'confirm-delete')
  { // 只有在非确认删除模式下才清空
    inputValue.value = '';
  }
});

// --- 事件处理 ---
function handleTriggerClick()
{
  isPopoverVisible.value = !isPopoverVisible.value;
  if (isPopoverVisible.value)
  {
    // 重置状态：对于删除模式，不需要重置输入值
    if (contentType.value !== 'confirm-delete')
    {
      inputValue.value = props.action.popoverInitialValue || '';
      selectValue.value = null;
    }
    nextTick(() =>
    {
      if (inputRef.value)
      {
        inputRef.value.focus();
      }
    });
  }
}

function handlePositiveClick()
{
  if (!isInputValid.value) return;

  // 根据 contentType 决定 payload 的内容
  if (contentType.value === 'confirm-delete')
  {
    emit('confirm', {}); // 确认删除，不带 name 和 type
  }
  else
  {
    const payload: { name: string, type?: string } = {
      name: inputValue.value.trim(),
    };
    if (contentType.value === 'select-and-input' && selectValue.value)
    {
      payload.type = selectValue.value;
    }
    emit('confirm', payload);
  }
  isPopoverVisible.value = false;
}

function handleNegativeClick()
{
  isPopoverVisible.value = false;
}

function handleClickOutside()
{
  isPopoverVisible.value = false;
}

defineExpose({
  handleTriggerClick,
  isPopoverVisible,
  handleClickOutside,
});
</script>