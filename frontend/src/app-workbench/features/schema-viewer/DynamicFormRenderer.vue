﻿<template>
  <n-spin :description="loadingDescription" :show="isLoading">
    <!-- Schema 加载错误 -->
    <div v-if="effectiveErrorMessage" style="padding: 20px;">
      <n-alert title="表单渲染错误" type="error">
        {{ effectiveErrorMessage }}
      </n-alert>
    </div>

    <!-- 动态表单 -->
    <vue-form
        v-else-if="processedSchema && typeof modelValue === 'object' && !effectiveErrorMessage"
        ref="jsonFormRef"
        :form-footer="{ show: false }"
        :form-props="formProps"
        :model-value="modelValue"
        :schema="processedSchema"
        style="flex-grow: 1;"
        @change="handleFormChange"
        @update:modelValue="handleFormUpdate"
    />

    <!-- Schema 未加载或数据类型不正确时的空状态 -->
    <n-empty v-else-if="!processedSchema && !isLoading" description="Schema 未提供或加载失败。"/>
    <n-empty v-else-if="typeof modelValue !== 'object' && !isLoading" description="表单数据格式不正确，请检查。"/>
  </n-spin>
</template>

<script lang="ts" setup>
import {computed, defineAsyncComponent, markRaw, ref} from 'vue';
import {NAlert, NEmpty, NSpin} from 'naive-ui';
import VueForm from '@lljj/vue3-form-naive';
import {cloneDeep} from "lodash-es";
import {preprocessSchemaForWidgets} from "@/app-workbench/features/schema-viewer/preprocessSchema.ts"; // 确认此库的准确导入名称和方式

// ----------- Props -----------
const props = defineProps({
  /**
   * 原始的 JSON Schema 对象。
   * DynamicFormRenderer 将对其进行预处理。
   */
  schema: {
    type: Object as () => Record<string, any> | null,
    required: true,
  },
  /**
   * 表单数据对象，用于 v-model。
   */
  modelValue: {
    type: Object as () => Record<string, any> | null, // 或者 Record<string, any> 如果更通用
    required: true,
  },
  /**
   * 传递给内部 Naive UI NForm 组件的属性。
   */
  formProps: {
    type: Object,
    default: () => ({
      labelPlacement: 'top' as const,
      labelWidth: 'auto' as const,
      isMiniDes: true, // 保持你原来的属性
    }),
  },
  /**
   * 控制加载状态的显示。
   */
  isLoading: {
    type: Boolean,
    default: false,
  },
  /**
   * 加载状态的描述文本。
   */
  loadingDescription: {
    type: String,
    default: '正在加载表单...',
  },
  /**
   * 用于显示外部传入的错误信息。
   */
  errorMessage: {
    type: String,
    default: null,
  },
});

// ----------- Emits -----------
const emit = defineEmits(['update:modelValue', 'change']);

// ----------- Refs -----------
const jsonFormRef = ref<any>(null); // vue-form 实例引用

// ----------- Computed Properties -----------
const processedSchema = computed(() =>
{
  if (props.schema)
  {
    try
    {
      // 如你所说，此处不再添加 showTitle/showDescription 的逻辑
      const result = preprocessSchemaForWidgets(props.schema);
      return result;
    } catch (error)
    {
      console.error('[DynamicFormRenderer] Error processing schema-viewer:', error);
      internalErrorMessage.value = `Schema 预处理失败: ${(error as Error).message}`;
      return null;
    }
  }
  return null;
});

const internalErrorMessage = ref<string | null>(null);
const effectiveErrorMessage = computed(() => props.errorMessage || internalErrorMessage.value);

// ----------- Event Handlers -----------
function handleFormUpdate(newValue: Record<string, any>)
{
  emit('update:modelValue', newValue);
}

function handleFormChange(eventPayload: any)
{
  // 直接将 vue-form 的 change 事件透传出去
  // eventPayload 通常包含 { value: newFormData, errors: validationErrors }
  emit('change', eventPayload);
}

// ----------- Exposed Methods -----------
defineExpose({
  /**
   * 校验表单。
   * @returns {Promise<void>} 如果校验成功，Promise 会 resolve。
   * @throws {Array<FormValidationError>} 如果校验失败，Promise 会 reject，并携带 Naive UI Form 的错误对象数组。
   */
  async validate(): Promise<void>
  {
    if (jsonFormRef.value && jsonFormRef.value.$$uiFormRef)
    {
      try
      {
        await jsonFormRef.value.$$uiFormRef.validate();
        return Promise.resolve();
      } catch (validationErrors)
      {
        console.warn('[DynamicFormRenderer] Validation failed:', validationErrors);
        return Promise.reject(validationErrors);
      }
    }
    else
    {
      const errorMsg = '[DynamicFormRenderer] Form instance not available for validation.';
      console.warn(errorMsg);
      return Promise.reject(new Error(errorMsg));
    }
  },

  /**
   * 清除表单的校验状态。
   */
  resetValidation()
  {
    if (jsonFormRef.value && jsonFormRef.value.$$uiFormRef &&
        typeof jsonFormRef.value.$$uiFormRef.restoreValidation === 'function')
    {
      jsonFormRef.value.$$uiFormRef.restoreValidation();
    }
    else
    {
      console.warn('[DynamicFormRenderer] Form instance not available for resetting validation.');
    }
  }
});

export interface DynamicFormRendererInstance
{
  validate: () => Promise<void>;
  resetValidation: () => void;
}

</script>

<style scoped>
/* 可以根据需要添加一些基础样式，或者保持为空 */
</style>