<template>
  <n-select
      v-model:value="model"
      :clearable="clearable"
      :disabled="disabled"
      :options="options"
      :placeholder="placeholder"
  />
</template>

<script lang="ts" setup>
import {NSelect, type SelectOption as NaiveSelectOption} from 'naive-ui';
import {useVModel} from "@vueuse/core";
import type {PropType} from 'vue';

const props = defineProps({
  // v-model 绑定的值
  modelValue: {
    type: [String, Number, null] as PropType<string | number | null>,
    default: null,
  },
  // 从 schema 的 enum 和 enumNames 生成的选项
  options: {
    type: Array as PropType<NaiveSelectOption[]>,
    default: () => [],
  },
  // 其他标准表单控件属性
  placeholder: {
    type: String,
    default: '请选择...',
  },
  disabled: {
    type: Boolean,
    default: false,
  },
  clearable: {
    type: Boolean,
    default: true,
  }
});

const emit = defineEmits(['update:modelValue']);

// 使用 useVModel 简化 v-model 的实现
const model = useVModel(props, 'modelValue', emit);
</script>