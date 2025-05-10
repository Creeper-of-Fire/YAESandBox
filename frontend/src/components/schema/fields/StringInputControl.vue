<template>
  <n-input
      :value="modelValue"
      :placeholder="fieldSchema.placeholder ?? undefined"
      :disabled="fieldSchema.isReadOnly || disabled"
      :type="inputTextType"
      :autosize="fieldSchema.schemaDataType === SchemaDataType.MULTILINE_TEXT ? { minRows: 3, maxRows: 5 } : undefined"
      @update:value="emit('update:modelValue', $event)"
      @blur="handleBlur"
  />
</template>

<script setup lang="ts">
import { computed, type PropType } from 'vue';
import { NInput } from 'naive-ui';
import { type FormFieldSchema, SchemaDataType } from '@/types/generated/aiconfigapi';

const props = defineProps({
  fieldSchema: {
    type: Object as PropType<FormFieldSchema>,
    required: true,
  },
  modelValue: {
    type: [String, null] as PropType<string | null>,
    default: '',
  },
  disabled: {
    type: Boolean,
    default: false,
  },
  // basePath: { // 如果需要触发单个字段校验，可能需要
  //   type: String,
  //   default: '',
  // },
});

const emit = defineEmits(['update:modelValue', 'blur']);

const inputTextType = computed(() => {
  if (props.fieldSchema.schemaDataType === SchemaDataType.MULTILINE_TEXT) return 'textarea';
  if (props.fieldSchema.schemaDataType === SchemaDataType.PASSWORD) return 'password';
  return 'text';
});

function handleBlur() {
  emit('blur'); // 冒泡 blur 事件给 SchemaDrivenForm，如果它需要处理
}
</script>