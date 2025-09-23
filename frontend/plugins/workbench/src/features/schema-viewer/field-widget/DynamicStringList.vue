<template>
  <n-dynamic-tags v-model:value="model" :max="max">
    <template #trigger="{ activate, disabled }">
      <n-button
          size="small"
          type="primary"
          dashed
          :disabled="disabled"
          @click="activate()"
      >
        <template #icon>
          <n-icon><add-icon /></n-icon>
        </template>
        {{ tagLabel }}
      </n-button>
    </template>

<!--    <template #input="{ value, index }">-->
<!--      <n-input-->
<!--          v-model:value="model![index]"-->
<!--          :placeholder="placeholder"-->
<!--          size="small"-->
<!--          @update:value="handleUpdate"-->
<!--      />-->
<!--    </template>-->
  </n-dynamic-tags>
</template>

<script lang="ts" setup>
import { NDynamicTags, NInput, NButton, NIcon } from 'naive-ui';
import {AddIcon } from '@yaesandbox-frontend/shared-ui/icons';
import { useVModel } from "@vueuse/core";
import { ref, watch } from "vue";

const props = withDefaults(defineProps<{
  modelValue: string[] | undefined;
  placeholder?: string;
  tagLabel?: string;
  max?: number;
}>(), {
  placeholder: '请输入值...',
  tagLabel: '添加一项'
});

const emit = defineEmits(['update:modelValue']);

// 使用 useVModel 来创建一个响应式的、可双向绑定的 model
// defaultValue: [] 确保即使 props.modelValue 初始为 undefined，组件内部也能正常工作
const model = useVModel(props, 'modelValue', emit, {
  passive: true,
  defaultValue: [],
});

// `n-dynamic-tags` 的 v-model:value 在删除项时可能会返回 null
// 我们需要一个 watcher 来确保它始终是一个数组
watch(model, (newValue) => {
  if (newValue === null) {
    model.value = [];
  }
});

// 当 n-input 更新时，确保我们发出的是一个干净的数组副本
// 这有助于避免一些 Vue 响应性的边界情况
const handleUpdate = () => {
  emit('update:modelValue', [...(model.value || [])]);
};
</script>