<template>
  <n-form
      ref="formRef"
      :model="internalModel"
      :rules="generatedRules"
      :label-width="labelWidth"
      :label-align="labelAlign"
      :label-placement="labelPlacement"
  >
    <template v-for="fieldSchema in sortedSchema" :key="fieldSchema.name">
      <n-form-item
          :label="fieldSchema.label"
          :path="getFieldPath(fieldSchema.name)"
          :rule="generatedRules[fieldSchema.name]"
      >
        <!-- 字段描述 Tooltip -->
        <template #label>
          {{ fieldSchema.label }}
          <n-tooltip v-if="fieldSchema.description" trigger="hover">
            <template #trigger>
              <n-icon style="margin-left: 4px; vertical-align: middle; cursor: help;">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024" width="14" height="14">
                  <path fill="currentColor"
                        d="M512 64a448 448 0 1 1 0 896a448 448 0 0 1 0-896m0 128a320 320 0 1 0 0 640a320 320 0 0 0 0-640m0 112c24.832 0 45.056 20.224 45.056 45.056v.064c0 24.832-20.224 45.056-45.056 45.056A45.12 45.12 0 0 1 467.008 304v-.064c0-24.832 20.16-45.056 44.992-45.056M534.4 672H480V448h54.4z"></path>
                </svg>
              </n-icon>
            </template>
            {{ fieldSchema.description }}
          </n-tooltip>
        </template>

        <!-- 各种输入控件 -->
        <template v-if="!isComplexType(fieldSchema.schemaDataType)">
          <n-input
              v-if="isInputType(fieldSchema.schemaDataType, ['STRING', 'MULTILINE_TEXT', 'PASSWORD', 'GUID'])"
              v-model:value="internalModel[fieldSchema.name]"
              :placeholder="fieldSchema.placeholder ?? undefined"
              :disabled="fieldSchema.isReadOnly || disabled"
              :type="getInputTextType(fieldSchema.schemaDataType)"
              :autosize="fieldSchema.schemaDataType === SchemaDataType.MULTILINE_TEXT ? { minRows: 3, maxRows: 5 } : undefined"
              @blur="() => handleFieldBlur(getFieldPath(fieldSchema.name))"
              @update:value="val => handleFieldUpdate(fieldSchema.name, val)"
          />
          <n-input-number
              v-else-if="isInputType(fieldSchema.schemaDataType, ['NUMBER', 'INTEGER'])"
              v-model:value="internalModel[fieldSchema.name]"
              :placeholder="fieldSchema.placeholder ?? undefined"
              :disabled="fieldSchema.isReadOnly || disabled"
              :precision="fieldSchema.schemaDataType === SchemaDataType.INTEGER ? 0 : undefined"
              :min="(fieldSchema.validation?.min as number | undefined)"
              :max="(fieldSchema.validation?.max as number | undefined)"
              style="width: 100%"
              @blur="() => handleFieldBlur(getFieldPath(fieldSchema.name))"
              @update:value="val => handleFieldUpdate(fieldSchema.name, val)"
          />
          <n-switch
              v-else-if="fieldSchema.schemaDataType === SchemaDataType.BOOLEAN"
              v-model:value="internalModel[fieldSchema.name]"
              :disabled="fieldSchema.isReadOnly || disabled"
              @update:value="val => handleFieldUpdate(fieldSchema.name, val)"
          />
          <n-select
              v-else-if="fieldSchema.schemaDataType === SchemaDataType.ENUM"
              v-model:value="internalModel[fieldSchema.name]"
              :placeholder="fieldSchema.placeholder ?? undefined"
              :options="getSelectOptions(fieldSchema)"
              :disabled="fieldSchema.isReadOnly || disabled"
              :tag="fieldSchema.isEditableSelectOptions"
              :filterable="true"
              :loading="loadingOptionsMap[fieldSchema.name]"
              clearable
              style="width: 100%"
              @update:value="val => handleFieldUpdate(fieldSchema.name, val)"
          />
          <n-date-picker
              v-else-if="fieldSchema.schemaDataType === SchemaDataType.DATE_TIME"
              v-model:value="internalModel[fieldSchema.name]"
              type="datetime"
              clearable
              :placeholder="fieldSchema.placeholder ?? undefined"
              :disabled="fieldSchema.isReadOnly || disabled"
              style="width: 100%"
              @blur="() => handleFieldBlur(getFieldPath(fieldSchema.name))"
              @update:value="val => handleFieldUpdate(fieldSchema.name, val)"
          />
          <n-text v-else-if="fieldSchema.schemaDataType === SchemaDataType.UNKNOWN" type="error">
            不支持的字段类型: {{ fieldSchema.name }} ({{ fieldSchema.schemaDataType }})
          </n-text>
        </template>

        <!-- 嵌套对象 Object -->
        <div v-else-if="fieldSchema.schemaDataType === SchemaDataType.OBJECT && fieldSchema.nestedSchema" class="nested-form-container">
          <SchemaDrivenForm
              :schema="fieldSchema.nestedSchema"
              v-model="internalModel[fieldSchema.name]"
              :disabled="fieldSchema.isReadOnly || disabled"
              :base-path="getFieldPath(fieldSchema.name)"
              :label-width="nestedLabelWidth"
              :label-align="labelAlign"
              :label-placement="nestedLabelPlacement || labelPlacement"
              @update:model-value="val => handleFieldUpdate(fieldSchema.name, val)"
          />
        </div>

        <!-- 数组 Array -->
        <template v-else-if="fieldSchema.schemaDataType === SchemaDataType.ARRAY && fieldSchema.arrayItemSchema">
          <n-dynamic-input
              v-model:value="internalModel[fieldSchema.name]"
              :on-create="() => createDefaultItem(fieldSchema.arrayItemSchema!)"
              :disabled="fieldSchema.isReadOnly || disabled"
              @update:value="val => handleFieldUpdate(fieldSchema.name, val)"
          >
            <template #default="{ value: arrayItemModel, index }">
              <!-- 如果数组项是简单类型 -->
              <template v-if="!isComplexType(fieldSchema.arrayItemSchema!.schemaDataType)">
                <n-input
                    v-if="isInputType(fieldSchema.arrayItemSchema!.schemaDataType, ['STRING', 'MULTILINE_TEXT', 'PASSWORD', 'GUID'])"
                    v-model:value="arrayItemModel[dynamicInputWorkaroundKey]"
                    :placeholder="fieldSchema.arrayItemSchema!.placeholder || '数组项'"
                    :type="getInputTextType(fieldSchema.arrayItemSchema!.schemaDataType)"
                    :disabled="fieldSchema.isReadOnly || disabled"
                />
                <n-input-number
                    v-else-if="isInputType(fieldSchema.arrayItemSchema!.schemaDataType, ['NUMBER', 'INTEGER'])"
                    v-model:value="arrayItemModel[dynamicInputWorkaroundKey]"
                    :placeholder="fieldSchema.arrayItemSchema!.placeholder || '数组项'"
                    :precision="fieldSchema.arrayItemSchema!.schemaDataType === SchemaDataType.INTEGER ? 0 : undefined"
                    :disabled="fieldSchema.isReadOnly || disabled"
                    style="width: 100%"
                />
                <!-- TODO: 为数组项添加更多简单类型的支持 (Boolean, Enum, DateTime) -->
              </template>
              <!-- 如果数组项是复杂对象 -->
              <div
                  v-else-if="fieldSchema.arrayItemSchema!.schemaDataType === SchemaDataType.OBJECT && fieldSchema.arrayItemSchema!.nestedSchema"
                  style="width:100%; padding: 5px; border: 1px dashed #ccc; margin-bottom: 5px;">
                <SchemaDrivenForm
                    :schema="fieldSchema.arrayItemSchema!.nestedSchema!"
                    :model-value="arrayItemModel"
                    @update:model-value="newValue => handleArrayItemUpdate(fieldSchema.name, index, newValue)"
                    :disabled="fieldSchema.isReadOnly || disabled"
                    :base-path="getArrayItemPath(fieldSchema.name, index)"
                    :label-width="nestedLabelWidth"
                    :label-align="labelAlign"
                    :label-placement="'left'"
                />
              </div>
            </template>
          </n-dynamic-input>
        </template>

        <!-- 字典 Dictionary -->
        <template
            v-else-if="fieldSchema.schemaDataType === SchemaDataType.DICTIONARY && fieldSchema.keyInfo && fieldSchema.dictionaryValueSchema">
          <n-dynamic-input
              v-model:value="internalModel[fieldSchema.name]"
              :on-create="() => ({ key: null, value: createDefaultItem(fieldSchema.dictionaryValueSchema!) })"
              item-style="padding-bottom: 10px;"
              :disabled="fieldSchema.isReadOnly || disabled"
              @update:value="(val) => handleFieldUpdate(fieldSchema.name, val)"
          >
            <template #default="{ value: dictEntry }">
              <div style="display: flex; align-items: flex-start; width: 100%; gap: 8px;">
                <!-- Key Input -->
                <n-form-item-gi :span="10" :path="`${getFieldPath(fieldSchema.name)}[${dictEntry.key}]._key_`" label="键"
                                label-placement="left" style="flex:1;">
                  <n-input
                      v-if="fieldSchema.keyInfo!.keyType === SchemaDataType.STRING"
                      v-model:value="dictEntry.key"
                      placeholder="键 (字符串)"
                      :disabled="fieldSchema.isReadOnly || disabled"
                  />
                  <n-select
                      v-else-if="fieldSchema.keyInfo!.keyType === SchemaDataType.ENUM && fieldSchema.keyInfo!.enumOptions"
                      v-model:value="dictEntry.key"
                      :options="fieldSchema.keyInfo!.enumOptions"
                      placeholder="键 (枚举)"
                      :disabled="fieldSchema.isReadOnly || disabled"
                      filterable
                  />
                  <n-input-number
                      v-else-if="isInputType(fieldSchema.keyInfo!.keyType, ['NUMBER', 'INTEGER'])"
                      v-model:value="dictEntry.key"
                      :placeholder="fieldSchema.keyInfo!.keyType === SchemaDataType.INTEGER ? '键 (整数)' : '键 (数字)'"
                      :precision="fieldSchema.keyInfo!.keyType === SchemaDataType.INTEGER ? 0 : undefined"
                      :disabled="fieldSchema.isReadOnly || disabled"
                      style="width:100%;"
                  />
                  <n-text v-else type="warning">不支持的字典键类型: {{ fieldSchema.keyInfo!.keyType }}</n-text>
                </n-form-item-gi>

                <!-- Value Input -->
                <n-form-item-gi :span="14" :path="`${getFieldPath(fieldSchema.name)}[${dictEntry.key}]._value_`" label="值"
                                label-placement="left" style="flex:2;">
                  <template v-if="!isComplexType(fieldSchema.dictionaryValueSchema!.schemaDataType)">
                    <n-input
                        v-if="isInputType(fieldSchema.dictionaryValueSchema!.schemaDataType, ['STRING', 'MULTILINE_TEXT', 'PASSWORD', 'GUID'])"
                        v-model:value="dictEntry.value"
                        :placeholder="fieldSchema.dictionaryValueSchema!.placeholder || '值'"
                        :type="getInputTextType(fieldSchema.dictionaryValueSchema!.schemaDataType)"
                        :disabled="fieldSchema.isReadOnly || disabled"
                    />
                    <n-input-number
                        v-else-if="isInputType(fieldSchema.dictionaryValueSchema!.schemaDataType, ['NUMBER', 'INTEGER'])"
                        v-model:value="dictEntry.value"
                        :placeholder="fieldSchema.dictionaryValueSchema!.placeholder || '值'"
                        :precision="fieldSchema.dictionaryValueSchema!.schemaDataType === SchemaDataType.INTEGER ? 0 : undefined"
                        :disabled="fieldSchema.isReadOnly || disabled"
                        style="width:100%"
                    />
                    <!-- TODO: 为字典值添加更多简单类型的支持 -->
                  </template>
                  <div
                      v-else-if="fieldSchema.dictionaryValueSchema!.schemaDataType === SchemaDataType.OBJECT && fieldSchema.dictionaryValueSchema!.nestedSchema"
                      style="width:100%; padding: 5px; border: 1px dashed #ccc;">
                    <SchemaDrivenForm
                        :schema="fieldSchema.dictionaryValueSchema!.nestedSchema!"
                        v-model="dictEntry.value"
                        :disabled="fieldSchema.isReadOnly || disabled"
                        :base-path="getDictItemValuePath(fieldSchema.name, dictEntry.key)"
                        :label-width="nestedLabelWidth"
                        :label-align="labelAlign"
                        :label-placement="'left'"
                    />
                  </div>
                </n-form-item-gi>
              </div>
            </template>
          </n-dynamic-input>
        </template>
      </n-form-item>
    </template>
  </n-form>
</template>

<script setup lang="ts">
import {computed, nextTick, onMounted, type PropType, ref, watch} from 'vue';
import {
  type FormInst,
  type FormItemRule,
  type FormRules,
  NDatePicker,
  NDynamicInput,
  NForm,
  NFormItem,
  NFormItemGi,
  NIcon,
  NInput,
  NInputNumber,
  NSelect,
  NSwitch,
  NText,
  NTooltip,
} from 'naive-ui';
import {type FormFieldSchema, SchemaDataType, type SelectOption as ApiSelectOption} from '@/types/generated/aiconfigapi'; // 假设你的类型文件在 './types'
import {OpenAPI} from '@/types/generated/aiconfigapi'; // 假设 OpenAPI 配置在此

// 解决 n-dynamic-input 中简单类型值无法直接绑定的问题
// n-dynamic-input 的 v-model:value 期望是一个对象数组，即使数组项只是一个简单值。
// 我们用一个固定的key来包装简单类型。
const dynamicInputWorkaroundKey = '_value_';


const props = defineProps({
  // 表单的 Schema 定义
  schema: {
    type: Array as PropType<FormFieldSchema[]>,
    required: true,
  },
  // 表单的双向绑定数据模型
  modelValue: {
    type: Object as PropType<Record<string, any>>,
    required: true,
  },
  // 是否禁用整个表单
  disabled: {
    type: Boolean,
    default: false,
  },
  // 基础路径，用于嵌套表单，以确保校验路径正确
  basePath: {
    type: String,
    default: '',
  },
  // 标签宽度
  labelWidth: {
    type: [String, Number] as PropType<string | number | undefined>,
    default: 'auto',
  },
  // 嵌套表单标签宽度
  nestedLabelWidth: {
    type: [String, Number] as PropType<string | number | undefined>,
    default: 80,
  },
  // 标签对齐方式
  labelAlign: {
    type: String as PropType<'left' | 'right' | undefined>,
    default: 'left',
  },
  // 标签位置
  labelPlacement: {
    type: String as PropType<'left' | 'top'>,
    default: 'top',
  },
  // 嵌套表单标签位置
  nestedLabelPlacement: {
    type: String as PropType<'left' | 'top' | undefined>,
    default: undefined, // 默认为空，会继承父级或使用嵌套表单自己的默认值
  },
});

const emit = defineEmits(['update:modelValue']);

const formRef = ref<FormInst | null>(null);
const internalModel = ref<Record<string, any>>({});
const generatedRules = ref<FormRules>({});
const loadingOptionsMap = ref<Record<string, boolean>>({}); // 存储每个字段选项的加载状态
const fieldOptionsMap = ref<Record<string, ApiSelectOption[]>>({}); // 存储从API加载的选项

// 确保表单项按 `order` 属性排序
const sortedSchema = computed(() => {
  return [...props.schema].sort((a, b) => (a.order || 0) - (b.order || 0));
});

// getFieldPath 函数简化并修正返回值类型
function getFieldPath(fieldName: string): string {
  if (props.basePath) {
    return `${props.basePath}.${fieldName}`;
  }
  return fieldName;
}

function getArrayItemPath(arrayFieldName: string, index: number): string {
  const fullArrayPath = getFieldPath(arrayFieldName); // e.g., "parent.myArray"
  return `${fullArrayPath}.${index}`; // e.g., "parent.myArray.0"
}

function getDictItemValuePath(dictFieldName: string, itemKey: string | number | null): string {
  const fullDictPath = getFieldPath(dictFieldName); // e.g., "parent.myDict"
  // 如果字典的键 (itemKey) 可能包含点 ('.')，这会与路径分隔符冲突。
  // 一个简单的处理方法是替换或编码这些点。
  const safeItemKey = String(itemKey ?? '_undefined_key_').replace(/\./g, '__DOT__'); // 将键中的 '.' 替换为 '__DOT__'
  return `${fullDictPath}.${safeItemKey}`; // e.g., "parent.myDict.some__DOT__key"
}


// 将 `FormFieldSchema` 的校验规则转换为 Naive UI 的 `FormItemRule`
function mapValidationRules(field: FormFieldSchema): FormItemRule[] {
  const rules: FormItemRule[] = [];
  const validation = field.validation;
  const baseMessage = validation?.errorMessage || `${field.label}无效`;

  // 必填规则
  if (field.isRequired) {
    const requiredRule: FormItemRule = {
      required: true,
      message: validation?.errorMessage || `${field.label}是必填项`,
    };
    // 根据类型设置触发器和类型
    switch (field.schemaDataType) {
      case SchemaDataType.STRING:
      case SchemaDataType.MULTILINE_TEXT:
      case SchemaDataType.PASSWORD:
      case SchemaDataType.GUID:
        requiredRule.trigger = ['input', 'blur'];
        requiredRule.type = 'string'; // 确保即使是空字符串也会触发校验
        break;
      case SchemaDataType.NUMBER:
      case SchemaDataType.INTEGER:
        requiredRule.trigger = ['input', 'blur'];
        requiredRule.type = 'number';
        break;
      case SchemaDataType.ENUM:
      case SchemaDataType.DATE_TIME:
        requiredRule.trigger = ['change', 'blur'];
        // type for ENUM can be string or number depending on option values
        if (field.schemaDataType === SchemaDataType.DATE_TIME) requiredRule.type = 'number'; // Naive UI date picker v-model:value is timestamp
        break;
      case SchemaDataType.BOOLEAN:
        requiredRule.trigger = ['change'];
        requiredRule.type = 'boolean';
        break;
      case SchemaDataType.ARRAY:
      case SchemaDataType.DICTIONARY: // 对于数组和字典，通常校验其长度或自定义校验
        requiredRule.trigger = ['change'];
        requiredRule.type = field.schemaDataType === SchemaDataType.ARRAY ? 'array' : 'object';
        requiredRule.validator = (rule, value) => {
          if (field.schemaDataType === SchemaDataType.ARRAY) {
            return value && value.length > 0;
          }
          if (field.schemaDataType === SchemaDataType.DICTIONARY) {
            // Dictionary is represented as Array<{key,value}> for n-dynamic-input
            // Or as Record<string,any> if not using n-dynamic-input directly for model
            // For this implementation (using array for n-dynamic-input):
            return value && Object.keys(value).length > 0 && value.every((item: any) => item.key !== null && item.key !== undefined && item.key !== '');
          }
          return true;
        }
        break;
      default:
        requiredRule.trigger = ['input', 'blur'];
    }
    rules.push(requiredRule);
  }

  // 其他校验规则
  if (validation) {
    const otherRule: FormItemRule = {trigger: ['input', 'blur']}; // 默认触发
    let hasOtherValidation = false;

    if (validation.min !== undefined && validation.min !== null && (field.schemaDataType === SchemaDataType.NUMBER || field.schemaDataType === SchemaDataType.INTEGER)) {
      otherRule.min = validation.min;
      otherRule.type = 'number';
      hasOtherValidation = true;
    }
    if (validation.max !== undefined && validation.max !== null && (field.schemaDataType === SchemaDataType.NUMBER || field.schemaDataType === SchemaDataType.INTEGER)) {
      otherRule.max = validation.max;
      otherRule.type = 'number';
      hasOtherValidation = true;
    }
    if (validation.minLength !== undefined && validation.minLength !== null && typeof internalModel.value[field.name] === 'string') {
      otherRule.min = validation.minLength;
      otherRule.type = 'string';
      hasOtherValidation = true;
    }
    if (validation.maxLength !== undefined && validation.maxLength !== null && typeof internalModel.value[field.name] === 'string') {
      otherRule.max = validation.maxLength;
      otherRule.type = 'string';
      hasOtherValidation = true;
    }
    if (validation.pattern) {
      if (validation.pattern.toLowerCase() === 'url') {
        rules.push({type: 'url', message: baseMessage, trigger: ['input', 'blur']});
      } else {
        try {
          otherRule.pattern = new RegExp(validation.pattern);
          otherRule.type = 'string'; // Pattern usually for strings
          hasOtherValidation = true;
        } catch (e) {
          console.warn(`[SchemaDrivenForm] 无效的正则表达式 for ${field.name}: ${validation.pattern}`, e);
        }
      }
    }
    if (hasOtherValidation) {
      otherRule.message = baseMessage;
      // Adjust trigger for non-string types if necessary
      if (otherRule.type === 'number') otherRule.trigger = ['input', 'blur'];
      rules.push(otherRule);
    }
  }
  return rules;
}

// 生成所有字段的校验规则
function regenerateAllRules() {
  const newRules: FormRules = {};
  props.schema.forEach(field => {
    // 路径处理：如果 basePath 存在，Naive UI 的 FormRules 的 key 应该是 'parent.child' 这种形式
    const pathKey = getFieldPath(field.name)
    newRules[pathKey] = mapValidationRules(field);
  });
  generatedRules.value = newRules;
}

// 初始化/更新内部模型
watch(
    () => props.modelValue,
    (newModel) => {
      const modelChanged = JSON.stringify(internalModel.value) !== JSON.stringify(newModel);
      if (modelChanged) {
        // console.log(`[SchemaDrivenForm ${props.basePath}] modelValue changed, updating internalModel`);
        const tempInternalModel: Record<string, any> = {};
        props.schema.forEach(field => {
          let valueToSet;
          if (newModel && newModel[field.name] !== undefined) {
            valueToSet = deepClone(newModel[field.name]);
          } else {
            valueToSet = createDefaultItem(field);
          }

          // 特殊处理 Dictionary: props.modelValue 是 Record<string, any>, internalModel 是 Array<{key, value}>
          if (field.schemaDataType === SchemaDataType.DICTIONARY) {
            if (typeof valueToSet === 'object' && valueToSet !== null && !Array.isArray(valueToSet)) {
              tempInternalModel[field.name] = Object.entries(valueToSet).map(([k, v]) => ({key: k, value: deepClone(v)}));
            } else if (Array.isArray(valueToSet)) { // Already in array format or default empty array
              tempInternalModel[field.name] = valueToSet?.map(item => ({...item, value: deepClone(item.value)}));
            } else {
              tempInternalModel[field.name] = [];
            }
          }
          // 特殊处理 Array of simple types for n-dynamic-input
          else if (field.schemaDataType === SchemaDataType.ARRAY && field.arrayItemSchema && !isComplexType(field.arrayItemSchema.schemaDataType)) {
            if (Array.isArray(valueToSet)) {
              tempInternalModel[field.name] = valueToSet.map(item => ({[dynamicInputWorkaroundKey]: deepClone(item)}));
            } else {
              tempInternalModel[field.name] = [];
            }
          } else {
            tempInternalModel[field.name] = valueToSet;
          }

        });
        internalModel.value = tempInternalModel;
      }
    },
    {immediate: true, deep: true}
);


// 监听 schema 变化（例如，动态 schema）
watch(
    () => props.schema,
    (newSchema) => {
      // console.log(`[SchemaDrivenForm ${props.basePath}] Schema changed, re-initializing...`);
      // 当 schema 变化时，需要重新初始化 model (保留现有值) 和规则
      const tempModel = {...internalModel.value}; // 保留当前值
      const newInternalModel: Record<string, any> = {};

      newSchema.forEach(field => {
        if (tempModel[field.name] !== undefined) {
          // 特殊转换同样适用于 schema 变化时保留值
          if (field.schemaDataType === SchemaDataType.DICTIONARY) {
            if (typeof tempModel[field.name] === 'object' && tempModel[field.name] !== null && !Array.isArray(tempModel[field.name])) {
              newInternalModel[field.name] = Object.entries(tempModel[field.name]).map(([k, v]) => ({key: k, value: deepClone(v)}));
            } else { // Assume already in array format or from previous internal state
              newInternalModel[field.name] = deepClone(tempModel[field.name]);
            }
          } else if (field.schemaDataType === SchemaDataType.ARRAY && field.arrayItemSchema && !isComplexType(field.arrayItemSchema.schemaDataType)) {
            if (Array.isArray(tempModel[field.name])) {
              // Check if items are already wrapped
              if (tempModel[field.name].length > 0 && typeof tempModel[field.name][0] === 'object' && tempModel[field.name][0].hasOwnProperty(dynamicInputWorkaroundKey)) {
                newInternalModel[field.name] = deepClone(tempModel[field.name]);
              } else {
                newInternalModel[field.name] = tempModel[field.name].map((item: any) => ({[dynamicInputWorkaroundKey]: deepClone(item)}));
              }
            } else {
              newInternalModel[field.name] = []; // Or default value
            }
          } else {
            newInternalModel[field.name] = deepClone(tempModel[field.name]);
          }
        } else {
          // 为新字段或 schema 中现有但 modelValue 中没有的字段设置默认值
          let defaultValue = createDefaultItem(field);
          if (field.schemaDataType === SchemaDataType.DICTIONARY) {
            // 确保字典是数组格式
            if (typeof defaultValue === 'object' && defaultValue !== null && !Array.isArray(defaultValue)) {
              newInternalModel[field.name] = Object.entries(defaultValue).map(([k, v]) => ({key: k, value: deepClone(v)}));
            } else { // default should be []
              newInternalModel[field.name] = [];
            }
          } else if (field.schemaDataType === SchemaDataType.ARRAY && field.arrayItemSchema && !isComplexType(field.arrayItemSchema.schemaDataType)) {
            if (Array.isArray(defaultValue)) {
              newInternalModel[field.name] = defaultValue.map(item => ({[dynamicInputWorkaroundKey]: deepClone(item)}));
            } else { // default should be []
              newInternalModel[field.name] = [];
            }
          } else {
            newInternalModel[field.name] = defaultValue;
          }
        }
        // 预加载动态选项
        if (field.optionsProviderEndpoint && !fieldOptionsMap.value[field.name]) {
          fetchOptions(field);
        }
      });
      internalModel.value = newInternalModel;
      regenerateAllRules(); // 重新生成校验规则
    },
    {immediate: true, deep: true}
);


// 字段值更新时，发出 update:modelValue 事件
function handleFieldUpdate(fieldName: string, value: any) {
  // console.log(`[SchemaDrivenForm ${props.basePath}] Field ${fieldName} updated to:`, value);
  // internalModel.value[fieldName] = value; // v-model 已经更新了 internalModel

  const outputModel = {...props.modelValue}; // 从 props.modelValue 开始构建，以保留不在当前 schema 中的字段

  // 将 internalModel 的值合并到 outputModel
  for (const key in internalModel.value) {
    const fieldSchema = props.schema.find(f => f.name === key);
    if (fieldSchema) {
      if (fieldSchema.schemaDataType === SchemaDataType.DICTIONARY) {
        // 将 Array<{key,value}> 转回 Record<string, any>
        const record: Record<string, any> = {};
        if (Array.isArray(internalModel.value[key])) {
          (internalModel.value[key] as Array<{ key: any, value: any }>).forEach(item => {
            if (item.key !== null && item.key !== undefined && String(item.key).trim() !== '') {
              record[String(item.key)] = item.value;
            }
          });
        }
        outputModel[key] = record;
      } else if (fieldSchema.schemaDataType === SchemaDataType.ARRAY && fieldSchema.arrayItemSchema && !isComplexType(fieldSchema.arrayItemSchema.schemaDataType)) {
        // 解包简单类型数组项
        if (Array.isArray(internalModel.value[key])) {
          outputModel[key] = (internalModel.value[key] as Array<any>).map(item => item[dynamicInputWorkaroundKey]);
        } else {
          outputModel[key] = [];
        }
      } else {
        outputModel[key] = internalModel.value[key];
      }
    }
  }
  emit('update:modelValue', outputModel);

  // 如果需要，可以在这里触发对单个字段的校验
  // nextTick(() => {
  //   formRef.value?.validate(
  //     undefined,
  //     (rule) => rule && rule.field === getFieldPath(fieldName) // 这可能不精确
  //   );
  // });
}

function handleArrayItemUpdate(arrayFieldName: string, index: number, newValue: any) {
  // internalModel.value[arrayFieldName] 是 n-dynamic-input 绑定的数组
  // 对于对象数组，其元素就是对象本身，可以直接赋值
  if (
      internalModel.value[arrayFieldName] &&
      Array.isArray(internalModel.value[arrayFieldName]) &&
      index >= 0 &&
      index < internalModel.value[arrayFieldName].length
  ) {
    // 直接修改内部模型数组中对应索引的项
    internalModel.value[arrayFieldName][index] = newValue;

    // 重要：由于我们直接修改了 internalModel 数组中的一个对象的内部，
    // 这可能不会触发外层 n-dynamic-input 的 @update:value 事件（它主要关心项的增删）。
    // 因此，我们需要手动调用 handleFieldUpdate 来确保整个数组字段的更新被正确地 emit 出去。
    // handleFieldUpdate 会从 internalModel 中取出 arrayFieldName 对应的整个数组 (现在已包含修改后的项),
    // 然后进行必要的转换 (例如字典格式转换) 并 emit('update:modelValue', ...)。
    handleFieldUpdate(arrayFieldName, internalModel.value[arrayFieldName]);
  } else {
    console.warn(
        `[SchemaDrivenForm ${props.basePath}] 尝试更新无效的数组成员: ${arrayFieldName}[${index}]`
    );
  }
}

function handleDictionaryUpdate(fieldName: string, value: Array<{ key: any, value: any }>) {
  // Dictionary 的 n-dynamic-input v-model:value 直接是数组，
  // internalModel[fieldName] 已经是这个数组。
  // handleFieldUpdate 会处理转换。
  // console.log(`[SchemaDrivenForm ${props.basePath}] Dictionary ${fieldName} updated:`, value);
  // 确保 key 的唯一性 (可选，或者在校验器中处理)
  // ...
  handleFieldUpdate(fieldName, value); // 触发上层更新和转换
}


// 字段失焦时，可以触发校验
function handleFieldBlur(path: string | string[] | undefined) {
  if (path && formRef.value) {
    formRef.value.validate(undefined, (rule) => {
      // Naive UI 的 rule.field 似乎没有被填充，所以我们用 path 来匹配
      // FormItem 的 path 如果是数组， validate 的第一个参数也需要是数组
      // 如果 path 是字符串 'obj.prop', FormItem 的 path 也是 'obj.prop'
      // 我们需要找到对应的 FormItem 的 path
      // 这里简单处理，如果 path 是字符串，直接用；如果是数组，直接用。
      // 但 FormRules 的 key 是字符串，FormItem 的 path 可以是数组。
      // 暂时无法精确匹配单个字段的 rule，先校验整个表单的特定路径
      // if (Array.isArray(path)) {
      //   return path.join('.') === rule.field; // this is not correct if rule.field is not set by Naive
      // }
      // return rule.field === path;
      return true; // For now, just re-validate all or rely on model change validation
    }).catch(() => { /* ignore validation errors on blur for now */
    });
  }
}


// 异步获取选项
async function fetchOptions(fieldSchema: FormFieldSchema) {
  if (!fieldSchema.optionsProviderEndpoint) return;
  loadingOptionsMap.value[fieldSchema.name] = true;
  try {
    // 假设 OpenAPI.BASE 已经配置好
    // 你可能需要一个通用的请求函数或使用生成的服务类
    const response = await fetch(OpenAPI.BASE + fieldSchema.optionsProviderEndpoint);
    if (!response.ok) {
      console.error(`[SchemaDrivenForm] Failed to fetch options from ${fieldSchema.optionsProviderEndpoint}: ${response.statusText}`);
      return;
    }
    fieldOptionsMap.value[fieldSchema.name] = await response.json() as ApiSelectOption[];
  } catch (error) {
    console.error(`[SchemaDrivenForm] Error fetching options for ${fieldSchema.name}:`, error);
    fieldOptionsMap.value[fieldSchema.name] = []; // 出错时设为空数组
  } finally {
    loadingOptionsMap.value[fieldSchema.name] = false;
  }
}

// 获取用于 Select 组件的选项
function getSelectOptions(fieldSchema: FormFieldSchema): ApiSelectOption[] {
  if (fieldSchema.optionsProviderEndpoint) {
    // 如果正在加载，可以返回一个包含加载状态的选项，或空数组
    // if (loadingOptionsMap.value[fieldSchema.name]) {
    //   return [{ label: '加载中...', value: '__loading__', disabled: true }];
    // }
    return fieldOptionsMap.value[fieldSchema.name] || fieldSchema.options || [];
  }
  return fieldSchema.options || [];
}

// 创建默认值
function createDefaultItem(fieldSchema: FormFieldSchema): any {
  if (fieldSchema.defaultValue !== undefined && fieldSchema.defaultValue !== null) {
    return deepClone(fieldSchema.defaultValue);
  }
  switch (fieldSchema.schemaDataType) {
    case SchemaDataType.STRING:
    case SchemaDataType.MULTILINE_TEXT:
    case SchemaDataType.PASSWORD:
    case SchemaDataType.GUID:
      return '';
    case SchemaDataType.NUMBER:
    case SchemaDataType.INTEGER:
      return null; // Or 0, depending on requirements
    case SchemaDataType.BOOLEAN:
      return false;
    case SchemaDataType.ENUM:
      // 默认选择第一个选项（如果optionsProviderEndpoint没有立即返回数据，这里可能为null）
      // const opts = getSelectOptions(fieldSchema);
      // return opts.length > 0 ? opts[0].value : null;
      return null; // 确保用户主动选择
    case SchemaDataType.DATE_TIME:
      return null; // Naive UI date picker v-model:value is timestamp (number) or null
    case SchemaDataType.OBJECT:
      if (fieldSchema.nestedSchema) {
        const obj: Record<string, any> = {};
        fieldSchema.nestedSchema.forEach(nestedField => {
          obj[nestedField.name] = createDefaultItem(nestedField);
        });
        return obj;
      }
      return {};
    case SchemaDataType.ARRAY:
      // 对于 n-dynamic-input 的简单类型数组，其 onCreate 返回的应该是包装对象
      if (fieldSchema.arrayItemSchema && !isComplexType(fieldSchema.arrayItemSchema.schemaDataType)) {
        return {[dynamicInputWorkaroundKey]: createDefaultItem(fieldSchema.arrayItemSchema)};
      }
      // 对于对象数组，onCreate 返回的是对象本身
      return fieldSchema.arrayItemSchema ? createDefaultItem(fieldSchema.arrayItemSchema) : {}; // Fallback for array item if complex
    case SchemaDataType.DICTIONARY:
      // n-dynamic-input for dictionary expects {key, value}
      // The parent model is an array of these. So `createDefaultItem` for dictionary field itself returns []
      return []; // Dictionary field itself defaults to an empty array of key-value pairs for n-dynamic-input
    default:
      return null;
  }
}

// 辅助函数：判断是否为复杂类型（需要特殊处理或递归渲染）
function isComplexType(type: SchemaDataType): boolean {
  return [SchemaDataType.OBJECT, SchemaDataType.ARRAY, SchemaDataType.DICTIONARY].includes(type);
}

// 辅助函数：判断是否为特定输入框类型
function isInputType(type: SchemaDataType, targetTypes: (keyof typeof SchemaDataType)[]): boolean {
  return targetTypes.some(targetType => SchemaDataType[targetType] === type);
}

// 辅助函数：获取 n-input 的 type 属性
function getInputTextType(type: SchemaDataType): 'text' | 'textarea' | 'password' {
  if (type === SchemaDataType.MULTILINE_TEXT) return 'textarea';
  if (type === SchemaDataType.PASSWORD) return 'password';
  return 'text';
}

// 深拷贝函数 (简易版，实际项目中可能使用 lodash.cloneDeep)
function deepClone<T>(obj: T): T {
  if (obj === null || typeof obj !== 'object') {
    return obj;
  }
  // Date objects
  if (obj instanceof Date) {
    return new Date(obj.getTime()) as any;
  }
  // Arrays
  if (Array.isArray(obj)) {
    const clonedArray = obj.map(item => deepClone(item));
    return clonedArray as any;
  }
  // Generic objects
  const clonedObj = {} as T;
  for (const key in obj) {
    if (Object.prototype.hasOwnProperty.call(obj, key)) {
      clonedObj[key] = deepClone(obj[key]);
    }
  }
  return clonedObj;
}


// 暴露给父组件的方法
defineExpose({
  validate: () => formRef.value?.validate(),
  restoreValidation: () => formRef.value?.restoreValidation(),
  resetFields: () => { // 重置为初始默认值或 modelValue 的初始值
    const initialModel: Record<string, any> = {};
    props.schema.forEach(field => {
      let valueToSet;
      // 如果 props.modelValue 提供了该字段的初始值，则使用它，否则使用 schema 的默认值
      if (props.modelValue && props.modelValue[field.name] !== undefined) {
        valueToSet = deepClone(props.modelValue[field.name]);
      } else {
        valueToSet = createDefaultItem(field);
      }
      // Transform for dictionary and simple array types
      if (field.schemaDataType === SchemaDataType.DICTIONARY) {
        if (typeof valueToSet === 'object' && valueToSet !== null && !Array.isArray(valueToSet)) {
          initialModel[field.name] = Object.entries(valueToSet).map(([k, v]) => ({key: k, value: deepClone(v)}));
        } else {
          initialModel[field.name] = Array.isArray(valueToSet) ? valueToSet?.map(item => ({...item, value: deepClone(item.value)})) : [];
        }
      } else if (field.schemaDataType === SchemaDataType.ARRAY && field.arrayItemSchema && !isComplexType(field.arrayItemSchema.schemaDataType)) {
        if (Array.isArray(valueToSet)) {
          initialModel[field.name] = valueToSet.map(item => ({[dynamicInputWorkaroundKey]: deepClone(item)}));
        } else {
          initialModel[field.name] = [];
        }
      } else {
        initialModel[field.name] = valueToSet;
      }
    });
    internalModel.value = initialModel;
    // 触发更新到父组件
    handleFieldUpdate('', null); // 用一个不存在的字段名触发一次全局 model 更新
    nextTick(() => {
      formRef.value?.restoreValidation();
    });
  }
});

// 在挂载时，为所有带 optionsProviderEndpoint 的字段触发选项加载
onMounted(() => {
  props.schema.forEach(field => {
    if (field.optionsProviderEndpoint && !fieldOptionsMap.value[field.name] && !loadingOptionsMap.value[field.name]) {
      fetchOptions(field);
    }
  });
});

// 提供 formRef 给子 SchemaDrivenForm 实例 (如果它们需要访问顶层 form)
// provide('parentFormRef', formRef);

</script>

<style scoped>
.nested-form-container {
  width: 100%;
  padding: 10px;
  border: 1px solid #eee;
  border-radius: 3px;
  margin-top: 5px;
}

/* 为 n-dynamic-input 内部的 n-form-item-gi 调整，避免标签重复或样式问题 */
:deep(.n-dynamic-input .n-form-item .n-form-item-label) {
  display: none; /* 在字典项内部，我们自己控制标签 */
}

:deep(.n-dynamic-input .n-form-item) {
  margin-bottom: 0 !important; /* 移除字典项内部 FormItem 的底部边距 */
}

</style>