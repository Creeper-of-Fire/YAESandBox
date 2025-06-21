<template>
  <n-spin :show="isLoading" :description="loadingDescription">
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
        :model-value="modelValue"
        @update:modelValue="handleFormUpdate"
        :schema="processedSchema"
        :form-props="formProps"
        :form-footer="{ show: false }"
        @change="handleFormChange"
        style="flex-grow: 1;"
    />

    <!-- Schema 未加载或数据类型不正确时的空状态 -->
    <n-empty v-else-if="!processedSchema && !isLoading" description="Schema 未提供或加载失败。"/>
    <n-empty v-else-if="typeof modelValue !== 'object' && !isLoading" description="表单数据格式不正确，请检查。"/>
  </n-spin>
</template>

<script setup lang="ts">
import {ref, computed, defineAsyncComponent, markRaw, toRefs} from 'vue';
import {NSpin, NAlert, NEmpty} from 'naive-ui';
import VueForm from '@lljj/vue3-form-naive'; // 确认此库的准确导入名称和方式
import type {AbstractAiProcessorConfig} from "@/app-workbench/types/generated/ai-config-api-client";

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

// ----------- Widget Imports (硬编码) -----------
// 这些保持和你原来组件中一致
const MyCustomStringAutoComplete = markRaw(defineAsyncComponent(() => import('@/app-workbench/features/schema-viewer/field-widget/MyCustomStringAutoComplete.vue')));
const SliderWithInputWidget = markRaw(defineAsyncComponent(() => import('@/app-workbench/features/schema-viewer/field-widget/SliderWithInputWidget.vue')));

// ----------- Schema Preprocessing Logic (直接从原组件迁移) -----------
/**
 * 预处理从后端获取的 JSON Schema，根据约定动态注入 ui:widget。
 * @param originalSchema 从后端获取的原始 JSON Schema 对象。
 * @returns 处理后、可供 vue-form 使用的 Schema 对象。
 */
function preprocessSchemaForWidgets(originalSchema: Record<string, any>): Record<string, any>
{
  // 深拷贝原始 Schema，避免修改原始对象
  const schema = JSON.parse(JSON.stringify(originalSchema));

  if (schema.properties)
  {
    for (const fieldName in schema.properties)
    {
      const fieldProps = schema.properties[fieldName];

      // 确保每个 property 都有一个 ui:options 对象，方便后续写入
      if (!fieldProps['ui:options'])
      {
        fieldProps['ui:options'] = {};
      }
      // 也可以直接在 uiSchema 层面操作（如果 vue-form 优先 uiSchema）
      // 但既然你提议对 ui:widget 赋值，直接修改 fieldProps 里的 ui:widget 更直接

      // 规则1: 有 maximum 和 minimum -> SliderWidget
      const fieldType = fieldProps.type;
      if ((typeof fieldType === 'string' && (fieldType === 'number' || fieldType === 'integer')) ||
          (Array.isArray(fieldType) && fieldType.some(t => t === 'number' || t === 'integer'))
      )
      {
        if (!fieldProps['ui:widget'])
        {
          if (fieldProps.hasOwnProperty('maximum') && fieldProps.hasOwnProperty('minimum'))
          {
            // 优先使用已有的 ui:widget (如果后端偶尔还是会传)，否则按约定赋值

            fieldProps['ui:options'].step = fieldProps['multipleOf'];
            fieldProps['ui:options'].default = fieldProps.default;
            fieldProps['ui:options'].max = fieldProps.maximum;
            fieldProps['ui:options'].min = fieldProps.minimum;
            if (fieldProps.type != 'integer')
              delete fieldProps['multipleOf'];
            fieldProps['ui:widget'] = SliderWithInputWidget; // 你需要确保 SliderWidget 已经注册或由库提供
          } else
          {
            fieldProps['ui:widget'] = 'InputNumberWidget';
            fieldProps['ui:options'].showButton = false;
          }
        }
      }

      // 规则2: 有 enum 和 enumNames
      if (fieldProps.enum && fieldProps.enumNames)
      {
        if (fieldProps['ui:options']?.isEditableSelectOptions === true)
        {
          if (!fieldProps['ui:widget'])
          { // 同样，优先用户已定义的
            fieldProps['ui:widget'] = MyCustomStringAutoComplete;
          }
        } else
        {
          if (!fieldProps['ui:widget'])
          {
            // 根据选项数量决定使用 Radio 还是 Select 可能是更好的实践
            // 但按你的约定，这里是 RadioWidget
            fieldProps['ui:widget'] = 'RadioWidget'; // 你需要确保 RadioWidget 已经注册或由库提供
          }
        }
      }
      // 你可以根据需要添加更多规则...

      // 清理临时的 isEditableSelectOptions，因为它只是一个标记，不应直接传递给 widget
      if (fieldProps['ui:options']?.hasOwnProperty('isEditableSelectOptions'))
      {
        // delete fieldProps['ui:options'].isEditableSelectOptions; // 看 vue-form 是否会把所有 ui:options 传给 widget
        fieldProps['ui:enumOptions'] = fieldProps['enum'].map((value: any, index: number) => ({
          label: fieldProps.enumNames[index] as string,
          value
        })) as Array<{ label: string, value: any }>;
        delete fieldProps['enum'];
        // 如果不删除，确保你的 MyCustomStringAutoComplete 和 RadioWidget 不会错误地使用这个标记
        // 通常，自定义 widget 会接收整个 ui:options 对象，所以如果标记只用于选择 widget，之后可以删除
      }
      // 如果 ui:options 为空，也可以考虑删除它
      if (Object.keys(fieldProps['ui:options']).length === 0)
      {
        delete fieldProps['ui:options'];
      }
    }
  }

  // 如果 Schema 中有 definitions (用于 $ref)，也可能需要递归处理它们
  // 但对于 ui:widget，通常只关心顶层 properties
  if (schema.definitions)
  {
    for (const defName in schema.definitions)
    {
      // 注意：这里递归调用 preprocessSchemaForWidgets(schema-viewer.definitions[defName])
      // 需要确保返回的是处理后的 definitions，并且原 schema-viewer 的 $ref 仍然有效。
      // 或者，一种更简单的方式是假设 ui:widget 仅应用于 schema-viewer.properties 中的字段，
      // 而不是 definitions 内部的字段。
      // 如果 definitions 内部的字段也需要通过 $ref 被渲染并应用这些规则，
      // 那么 vue-form 在解析 $ref 时，如果能应用外层传递的 widgets 映射，就无需递归。
      // 否则，递归处理 definitions 是必要的。
      // 为简单起见，我们先假设主要处理 schema-viewer.properties
      schema.definitions[defName] = preprocessSchemaForWidgets(schema.definitions[defName]);
    }
  }

  return schema;
}

// ----------- Computed Properties -----------
const processedSchema = computed(() =>
{
  if (props.schema)
  {
    try
    {
      // 如你所说，此处不再添加 showTitle/showDescription 的逻辑
      return preprocessSchemaForWidgets(props.schema);
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
function handleFormUpdate(newValue: AbstractAiProcessorConfig)
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
    } else
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
    } else
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