<template>
  <n-radio-group v-model:value="model" name="radiogroup">
    <n-space>
      <n-radio
          v-for="option in options"
          :key="option.value.toString()"
          :value="option.value"
      >
        {{ option.label }}
      </n-radio>
    </n-space>
  </n-radio-group>
</template>

<script lang="ts" setup>
import { computed } from 'vue';
import { NRadioGroup, NRadio, NSpace } from 'naive-ui';

interface RadioOption {
  label: string;
  value: string | number | boolean;
}

const props = defineProps<{
  modelValue: string | number | boolean | undefined;
  options: RadioOption[];
}>();

const emit = defineEmits(['update:modelValue']);

// 使用 computed 属性代理 v-model
const model = computed({
  get: () => props.modelValue,
  set: (value) => {
    emit('update:modelValue', value);
  }
});
</script>