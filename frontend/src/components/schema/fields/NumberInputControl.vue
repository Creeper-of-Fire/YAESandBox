<template>
  <div v-if="showSlider" style="display: flex; align-items: center; width: 100%; gap: 16px;">
    <n-slider
        :value="modelValue??undefined"
        :min="sliderMin"
        :max="sliderMax"
        :step="sliderStep"
        :disabled="fieldSchema.isReadOnly || disabled"
        style="flex: 1;"
        @update:value="emit('update:modelValue', $event)"
    />
    <n-input-number
        :value="modelValue"
        :placeholder="fieldSchema.placeholder ?? undefined"
        :disabled="fieldSchema.isReadOnly || disabled"
        :precision="precision"
        :min="validationMin"
        :max="validationMax"
        :step="inputStep"
        style="width: 120px;"
        @update:value="emit('update:modelValue', $event)"
        @blur="handleBlur"
    />
  </div>
  <n-input-number
      v-else
      :value="modelValue"
      :placeholder="fieldSchema.placeholder ?? undefined"
      :disabled="fieldSchema.isReadOnly || disabled"
      :precision="precision"
      :min="validationMin"
      :max="validationMax"
      :step="inputStep"
      style="width: 100%"
      @update:value="emit('update:modelValue', $event)"
      @blur="handleBlur"
  />
</template>

<script setup lang="ts">
import {computed, type PropType} from 'vue';
import {NInputNumber, NSlider} from 'naive-ui';
import {type FormFieldSchema, SchemaDataType} from '@/types/generated/aiconfigapi';

const props = defineProps({
  fieldSchema: {
    type: Object as PropType<FormFieldSchema>,
    required: true,
  },
  modelValue: {
    type: [Number, null] as PropType<number | null>,
    default: null,
  },
  disabled: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['update:modelValue', 'blur']);

const validationMin = computed(() => props.fieldSchema.validation?.min as number | undefined);
const validationMax = computed(() => props.fieldSchema.validation?.max as number | undefined);
const schemaStep = computed(() => props.fieldSchema.validation?.step as number | undefined); // 假设 step 可能在 validation 中定义

const precision = computed(() => (props.fieldSchema.schemaDataType === SchemaDataType.INTEGER ? 0 : undefined));
const inputStep = computed(() => (props.fieldSchema.schemaDataType === SchemaDataType.INTEGER ? 1 : (schemaStep.value ?? 0.01)));


const showSlider = computed(() => {
  const min = validationMin.value;
  const max = validationMax.value;
  return min != null && max != null && min < max;
});

const sliderMin = computed(() => validationMin.value ?? 0); // Slider 需要确切的 min/max
const sliderMax = computed(() => validationMax.value ?? 100); // Slider 需要确切的 min/max
const sliderStep = computed(() => inputStep.value);


function handleBlur() {
  emit('blur');
}
</script>