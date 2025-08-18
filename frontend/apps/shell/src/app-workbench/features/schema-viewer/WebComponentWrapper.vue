<template>
  <component :is="tagName" ref="wcRef" :value="modelValue" @input="onInput"></component>
</template>

<script lang="ts" setup>
import { ref, watch } from 'vue';

const props = defineProps({
  modelValue: {
    type: [String, Number, Boolean, Object, Array],
    default: undefined
  },
  // 从 ui:options 传入的 Web Component 标签名
  tagName: {
    type: String,
    required: true
  }
});

const emit = defineEmits(['update:modelValue']);
const wcRef = ref<HTMLElement | null>(null);

// 将 Vue 的 modelValue 同步到 Web Component 的 value 属性
watch(() => props.modelValue, (newValue) => {
  if (wcRef.value && (wcRef.value as any).value !== newValue) {
    (wcRef.value as any).value = newValue;
  }
});

// 监听 Web Component 的 input 事件，更新 Vue 的 modelValue
function onInput(event: CustomEvent) {
  emit('update:modelValue', event.detail);
}
</script>