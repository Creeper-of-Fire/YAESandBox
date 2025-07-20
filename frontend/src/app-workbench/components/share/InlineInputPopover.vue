<!-- src/app-workbench/components/share/InlineInputPopover.vue -->
<template>
  <n-popover
      v-model:show="isPopoverVisible"
      :style="{ width: '300px' }"
      placement="right"
      trigger="manual"
  >
    <template #trigger>
      <!-- 外部通过插槽传入触发器，比如一个按钮 -->
      <div @click="handleTriggerClick">
        <slot></slot>
      </div>
    </template>

    <!-- Popover 内部的内容 -->
    <n-flex vertical>
      <n-h5 style="margin: 0 0 8px 0;">{{ title }}</n-h5>

      <!-- 模块类型选择 (仅在 content-type 为 'select-and-input' 时显示) -->
      <n-form-item v-if="contentType === 'select-and-input'" label="类型" required>
        <n-select
            v-model:value="selectValue"
            :options="selectOptions"
            :placeholder="selectPlaceholder"
            filterable
        />
      </n-form-item>

      <!-- 名称输入 -->
      <n-form-item v-if="showNameInput" label="名称" required>
        <n-input
            ref="inputRef"
            v-model:value="inputValue"
            :placeholder="inputPlaceholder"
            @keydown.enter.prevent="handlePositiveClick"
        />
      </n-form-item>

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
import { computed, nextTick, ref, watch } from 'vue';
import { NButton, NFlex, NFormItem, NH5, NInput, NPopover, NSelect, type SelectOption } from 'naive-ui';
import type { InputInst } from 'naive-ui';

const props = withDefaults(defineProps<{
  title: string;
  positiveText?: string;
  negativeText?: string;
  // --- 输入框相关 ---
  inputPlaceholder?: string;
  initialValue?: string;
  // --- Popover 内容类型 ---
  contentType?: 'input' | 'select-and-input';
  // --- Select 相关 (当 contentType 为 'select-and-input' 时使用) ---
  selectOptions?: SelectOption[];
  selectPlaceholder?: string;
  // --- 默认名称相关 ---
  // 用于根据 select 的选择动态生成默认名称
  defaultNameGenerator?: (selectedValue: any, selectOptions: SelectOption[]) => string;
}>(), {
  positiveText: '确认',
  negativeText: '取消',
  inputPlaceholder: '请输入内容',
  initialValue: '',
  contentType: 'input',
  selectOptions: () => [],
  selectPlaceholder: '请选择',
});

const emit = defineEmits<{
  (e: 'confirm', payload: { name: string; type?: string }): void;
}>();

// --- 内部状态 ---
const isPopoverVisible = ref(false);
const inputRef = ref<InputInst | null>(null);
const inputValue = ref('');
const selectValue = ref<string | null>(null);

// --- 计算属性 ---
const showNameInput = computed(() => {
  // 如果是简单输入模式，则始终显示
  if (props.contentType === 'input') return true;
  // 如果是选择+输入模式，则仅在选择了类型后显示
  return !!selectValue.value;
});

const isInputValid = computed(() => {
  if (!showNameInput.value) return false;
  return inputValue.value.trim() !== '';
});

// --- 监视器 ---
watch(selectValue, (newType) => {
  if (newType) {
    if (props.defaultNameGenerator) {
      inputValue.value = props.defaultNameGenerator(newType, props.selectOptions || []);
    } else {
      // 默认行为：从选项的 label 推断
      const option = props.selectOptions.find(opt => opt.value === newType);
      inputValue.value = option?.label as string || '新项目';
    }
  } else {
    inputValue.value = '';
  }
});

// --- 事件处理 ---
async function handleTriggerClick() {
  isPopoverVisible.value = !isPopoverVisible.value;
  if (isPopoverVisible.value) {
    // 重置状态
    inputValue.value = props.initialValue;
    selectValue.value = null;
    await nextTick();
    if (inputRef.value) {
      inputRef.value.focus();
    }
  }
}

defineExpose({
  handleTriggerClick
});

function handlePositiveClick() {
  if (!isInputValid.value) return;

  const payload: { name: string, type?: string } = {
    name: inputValue.value.trim(),
  };

  if (props.contentType === 'select-and-input' && selectValue.value) {
    payload.type = selectValue.value;
  }

  emit('confirm', payload);
  isPopoverVisible.value = false;
}

function handleNegativeClick() {
  isPopoverVisible.value = false;
}
</script>