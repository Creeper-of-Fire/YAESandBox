<template>
  <n-spin :show="isLoading">
    <div v-if="effectiveErrorMessage">
      <NAlert type="error">{{ effectiveErrorMessage }}</NAlert>
    </div>

    <VeeForm ref="veeFormRef" :initial-values="modelValue??undefined">
      <GroupedFieldRenderer v-slot="{ field, group }" :fields="formFields">
        <FormFieldWrapper
            :description="field.description"
            :isSingleInGroup="group.fields.length === 1 && !group.groupName"
            :label="field.label"
            :name="field.name"
        >
          <Field v-slot="{ value, errors, handleChange, handleBlur }: FieldSlotProps" :name="field.name"
                 :rules="field.rules.join('|')"
          >
            <component
                :is="componentMap[field.component]"
                :id="field.name"
                :component-map="componentMap"
                :modelValue="value"
                :name="field.name"
                :status="errors.length > 0 ? 'error' : undefined"
                :value="value"
                v-bind="field.props"
                @blur="handleBlur"
                @update:value="handleValueUpdate($event, handleChange)"
                @update:modelValue="handleValueUpdate($event, handleChange)"
            />
          </Field>
        </FormFieldWrapper>
      </GroupedFieldRenderer>
    </VeeForm>
  </n-spin>
</template>
<!-- TODO 这堆响应式有bug，但是目前先不管 -->
<script lang="ts" setup>
import {type Component, computed, nextTick, ref, shallowRef, watch} from 'vue';
import {configure, defineRule, Field, type FieldSlotProps, Form as VeeForm} from 'vee-validate';
import {all} from '@vee-validate/rules';
import {localize} from '@vee-validate/i18n';
import zh_CN from '@vee-validate/i18n/dist/locale/zh_CN.json';
import {type FormFieldViewModel, preprocessSchemaForVeeValidate} from '#/features/schema-viewer/preprocessSchema.ts';
import FormFieldWrapper from "#/features/schema-viewer/FormFieldWrapper.vue";
import {NSpin, useThemeVars} from "naive-ui";
import {isArray, isEqual, mergeWith, set} from 'lodash-es';
import GroupedFieldRenderer from "#/features/schema-viewer/GroupedFieldRenderer.vue";

// --- VeeValidate 全局配置 ---
Object.keys(all).forEach(rule =>
{
  defineRule(rule, (all as any)[rule]);
});
configure({
  generateMessage: localize('zh_CN', zh_CN),
  validateOnInput: true,
});

// --- Props ---
const props = defineProps({
  schema: {
    type: Object as () => Record<string, any> | null,
    required: true,
  },
  modelValue: {
    type: Object as () => Record<string, any> | null,
    required: true,
  },
  isLoading: {
    type: Boolean,
    default: false,
  },
  loadingDescription: {
    type: String,
    default: '正在加载表单...',
  },
  errorMessage: {
    type: String,
    default: null,
  },
});

// --- Emits ---
const emit = defineEmits(['update:modelValue', 'change']);

// --- 内部状态 ---
const veeFormRef = ref<InstanceType<typeof VeeForm> | null>(null);
const formFields = ref<FormFieldViewModel[]>([]);
const componentMap = shallowRef<Record<string, Component>>({});
const internalErrorMessage = ref<string | null>(null);

const schemaDefaults = ref<Record<string, any>>({});

const effectiveErrorMessage = computed(() => props.errorMessage || internalErrorMessage.value);

// 自定义合并逻辑
function customizer(objValue: any, srcValue: any)
{
  if (isArray(srcValue))
  {
    return srcValue; // 如果源值是数组，直接返回它（替换）
  }
  // 对于其他类型，回退到 lodash 的默认合并行为
}

// --- 核心逻辑: 监听 Schema 变化并重新处理 ---
watch(() => props.schema,
    async (newSchema) =>
    {
      if (!newSchema)
      {
        formFields.value = [];
        return;
      }

      console.log('[DynamicForm] Schema changed, rebuilding form...');
      try
      {
        // 步骤 1: 重新解析 Schema，更新字段和组件
        const {fields, componentMap: map} = preprocessSchemaForVeeValidate(newSchema);
        formFields.value = fields;
        componentMap.value = map;
        internalErrorMessage.value = null;

        // 步骤 2: 从新的字段中提取默认值，并存储起来
        schemaDefaults.value = buildDefaultsFromFields(fields);

        // 步骤 3: 结合当前 modelValue 和新的 schema 默认值，计算出完整的初始值
        const initialValues = mergeWith({}, schemaDefaults.value, props.modelValue ?? {}, customizer);

        // 步骤 4: 等待 DOM 更新后，使用 VeeValidate API 重置整个表单
        await nextTick();
        if (veeFormRef.value)
        {
          veeFormRef.value.resetForm({
            values: initialValues,
          });
        }

      } catch (error)
      {
        console.error('[DynamicFormRenderer] Error processing schema:', error);
        internalErrorMessage.value = `Schema 预处理失败: ${(error as Error).message}`;
        formFields.value = [];
      }
    },
    {immediate: true, deep: true}
);

watch(() => props.modelValue,
    (newModelValue) =>
    {
      if (!veeFormRef.value) return;

      // [关键] 防止无限更新循环：
      // 当我们 emit('update:modelValue') 时，会触发这个 watcher。
      // 如果新值与表单内部的当前值相同，说明这次变更是由组件内部引起的，无需再从外部同步。
      const currentFormValues = veeFormRef.value.getValues();
      if (isEqual(currentFormValues, newModelValue))
      {
        return;
      }

      console.log('[DynamicForm] External modelValue changed, syncing values...');
      // 只更新值，不重新解析 schema
      const valuesToSet = mergeWith({}, schemaDefaults.value, newModelValue ?? {}, customizer);
      veeFormRef.value.resetForm({
        values: valuesToSet,
      });
    },
    {deep: true} // 不需要 immediate，因为 schema watcher 的 immediate 运行会处理初始状态
);

/**
 * 辅助函数：根据字段定义构建一个包含默认值的对象
 * @param fields - 预处理后的字段数组
 */
function buildDefaultsFromFields(fields: FormFieldViewModel[]): Record<string, any>
{
  const defaults: Record<string, any> = {};
  fields.forEach(field =>
  {
    if (field.initialValue !== undefined)
    {
      // 使用 lodash.set 来处理嵌套路径，例如 'user.address.street'
      set(defaults, field.name, field.initialValue);
    }
  });
  return defaults;
}


// --- 事件处理 ---
async function handleValueUpdate(
    newValue: any,
    veeHandleChange: (value: any) => void
)
{
  let processedValue = newValue;

  // 使用处理后的值调用 vee-validate 的更新函数
  veeHandleChange(processedValue);

  // 等待 Vue 更新 DOM 和 VeeValidate 状态同步
  await nextTick();

  // 从 VeeValidate 获取最新的完整表单数据和错误状态
  if (veeFormRef.value)
  {
    const currentValues = veeFormRef.value.getValues();
    const currentErrors = veeFormRef.value.errors;

    const mergedData = mergeWith({}, props.modelValue, currentValues, customizer);

    // 立即向外触发事件，实现 v-model 和 @change
    emit('update:modelValue', mergedData);
    emit('change', {value: mergedData, errors: currentErrors});
  }
}

// --- Expose API ---
defineExpose({
  // validate 现在返回一个包含校验状态的对象
  async validate(): Promise<{ valid: boolean; errors: Record<string, string | undefined> }>
  {
    if (!veeFormRef.value)
    {
      console.warn('Form instance not available for validation.');
      return {valid: true, errors: {}}; // 或者 false，根据业务决定
    }
    const result = await veeFormRef.value.validate();
    return {
      valid: result.valid,
      errors: result.errors,
    };
  },

  // 用于在校验通过后获取数据
  getValues(): Record<string, any> | undefined
  {
    return veeFormRef.value?.getValues();
  },

  resetValidation()
  {
    if (veeFormRef.value)
    {
      veeFormRef.value.resetForm();
    }
  }
});

export interface DynamicFormRendererInstance
{
  validate: () => Promise<{ valid: boolean; errors: Record<string, string | undefined> }>;
  resetValidation: () => void;
  getValues: () => Record<string, any> | undefined;
}

const themeVars = useThemeVars();
</script>

<style>
.form-group-wrapper {
  --form-divider-margin-top: 1px;
  --form-divider-margin-bottom: 0.5rem;
}
</style>