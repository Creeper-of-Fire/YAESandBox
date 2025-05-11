// MyCustomStringAutoComplete.vue
<template>
  <n-auto-complete
      :value="internalValue ?? undefined"
      :options="computedOptions"
      :placeholder="computedPlaceholder"
      :disabled="props.disabled"
      :readonly="props.readonly"
      @update:value="handleUpdateValue"
      clearable
      blur-after-select
  />
</template>

<script setup lang="ts">
import {computed, ref, watch, defineProps, defineEmits, useAttrs} from 'vue';
import {NAutoComplete} from 'naive-ui';
import type {AutoCompleteOption} from 'naive-ui';

// 核心 props (基于 v-model 和通用状态)
const props = defineProps<{
  modelValue: string | null | undefined; // Vue 3 v-model prop
  disabled?: boolean;
  readonly?: boolean;
  enumOptions?: Array<{ label: string, value: any }>;
  // --- 根据文档，其他 ui:xxx 应该作为 props ---
  // 例如，如果 schema 中有 "ui:placeholder": "...", 则这里应该有 placeholder prop
  placeholder?: string;
  // options?: Record<string, any>; // 如果 ui:options 整体作为 'options' prop 传递
}>();

const emit = defineEmits(['update:modelValue']);
// const attrs = useAttrs(); // 获取所有未在 props 中声明的属性

const internalValue = ref(props.modelValue);

watch(() => props.modelValue, (newValue) => {
  internalValue.value = newValue;
});

const computedOptions = computed<Array<{ label: string, value: any }>>(() => {
  // **核心改动：从 attrs 中获取 enumOptions**
  if (props.enumOptions && Array.isArray(props.enumOptions)) {
    // 或者需要简单转换，例如确保 value 是 string
    return (props.enumOptions as any[]).map(opt => ({
      label: String(opt.label || opt.value), // 确保 label 存在
      value: String(opt.value) // NAutoComplete 的 value 通常是 string
    }));
  }
  console.warn('MyCustomStringAutoComplete: enumOptions not found in props or is not an array.');
  return [];
});

// Placeholder 的获取逻辑：优先 props.placeholder (来自 ui:placeholder)，然后是 attrs.placeholder (如果库这样传)，最后是 schema 中的 description (如果能获取到 schema)
const computedPlaceholder = computed<string>(() => {
  return props.placeholder ? props.placeholder : '';
});

const handleUpdateValue = (val: string) => {
  internalValue.value = val;
  emit('update:modelValue', val);
};
</script>