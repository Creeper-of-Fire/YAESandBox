<template>
  <n-radio-group v-model:value="model" name="radiogroup">
    <n-flex>
      <n-radio
          v-for="option in options"
          :key="option.value.toString()"
          :value="option.value"
      >
        {{ option.label }}
      </n-radio>
    </n-flex>
  </n-radio-group>
</template>

<script lang="ts" setup>
import {NRadio, NRadioGroup, NFlex} from 'naive-ui';
import {useVModel} from "@vueuse/core";

interface RadioOption
{
  label: string;
  value: string | number | boolean;
}

const props = defineProps<{
  modelValue: string | number | boolean | undefined;
  options: RadioOption[];
}>();

const emit = defineEmits(['update:modelValue']);

const model = useVModel(props, 'modelValue', emit);
</script>