<template>
  <n-select
      :value="modelValue"
      :placeholder="fieldSchema.placeholder ?? undefined"
      :options="selectOptions"
      :disabled="fieldSchema.isReadOnly || disabled"
      :tag="isTagMode"
      :filterable="true"
      :loading="isLoadingOptions"
      clearable
      style="width: 100%"
      @update:value="emit('update:modelValue', $event)"
  />
</template>

<script setup lang="ts">
import { computed, onMounted, ref, type PropType } from 'vue';
import { NSelect } from 'naive-ui';
import { type FormFieldSchema, type SelectOption as ApiSelectOption, SchemaDataType } from '@/types/generated/aiconfigapi';
import { OpenAPI } from '@/types/generated/aiconfigapi'; // 假设 OpenAPI 配置在此

const props = defineProps({
  fieldSchema: {
    type: Object as PropType<FormFieldSchema>,
    required: true,
  },
  modelValue: {
    type: [String, Number, null] as PropType<string | number | null>, // Enum value can be string or number
    default: null,
  },
  disabled: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['update:modelValue']);

const isLoadingOptions = ref(false);
const fetchedOptions = ref<ApiSelectOption[] | null>(null);

const isTagMode = computed(() => {
  // ENUM 类型如果 isEditableSelectOptions=true, 也是 tag 模式
  // STRING 类型如果提供了 options 且 isEditableSelectOptions=true, 也应该是 tag 模式 (由 SchemaDrivenForm 的 resolveFieldComponent 决定是否用这个组件)
  return props.fieldSchema.isEditableSelectOptions;
});

const selectOptions = computed(() => {
  if (props.fieldSchema.optionsProviderEndpoint) {
    return fetchedOptions.value || props.fieldSchema.options || [];
  }
  return props.fieldSchema.options || [];
});

async function fetchOptionsIfNeeded() {
  if (props.fieldSchema.optionsProviderEndpoint && !fetchedOptions.value) {
    isLoadingOptions.value = true;
    try {
      const response = await fetch(OpenAPI.BASE + props.fieldSchema.optionsProviderEndpoint);
      if (!response.ok) {
        console.error(`[EnumSelectControl] Failed to fetch options from ${props.fieldSchema.optionsProviderEndpoint}: ${response.statusText}`);
        fetchedOptions.value = []; // or props.fieldSchema.options as fallback
        return;
      }
      fetchedOptions.value = await response.json() as ApiSelectOption[];
    } catch (error) {
      console.error(`[EnumSelectControl] Error fetching options for ${props.fieldSchema.name}:`, error);
      fetchedOptions.value = []; // or props.fieldSchema.options as fallback
    } finally {
      isLoadingOptions.value = false;
    }
  }
}

onMounted(() => {
  fetchOptionsIfNeeded();
});

// Watch for schema changes that might affect options (e.g., dynamic endpoint)
// watch(() => props.fieldSchema.optionsProviderEndpoint, () => {
//   fetchedOptions.value = null; // Reset to allow re-fetch
//   fetchOptionsIfNeeded();
// });
</script>