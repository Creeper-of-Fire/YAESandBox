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
          :rule="generatedRules[getFieldPath(fieldSchema.name)]"
      >
        <!-- 字段描述 Tooltip -->
        <template #label>
          {{ fieldSchema.label }}
          <FieldDescriptionTooltip :description="fieldSchema.description"/>
        </template>

        <!-- 动态渲染特定类型的字段组件 -->
        <component
            :is="resolveFieldComponent(fieldSchema)"
            :field-schema="fieldSchema"
            :model-value="internalModel[fieldSchema.name]"
            @update:modelValue="newValue => handleFieldValueUpdate(fieldSchema.name, newValue)"
            :disabled="fieldSchema.isReadOnly || disabled"
            :base-path="getFieldPath(fieldSchema.name)"
            :label-width="nestedLabelWidth"
            :label-align="labelAlign"
            :label-placement="nestedLabelPlacement || labelPlacement"
            @blur="() => handleFieldBlur(getFieldPath(fieldSchema.name))"
        />
      </n-form-item>
    </template>
  </n-form>
</template>

<script setup lang="ts">
import {
  ref,
  computed,
  watch,
  nextTick,
  type PropType,
  type Component as VueComponent
} from 'vue';
import {
  NForm,
  NFormItem,
  type FormInst,
  type FormRules,
  type FormItemRule,
} from 'naive-ui';
import {type FormFieldSchema, SchemaDataType} from '@/types/generated/aiconfigapi';
import FieldDescriptionTooltip from './FieldDescriptionTooltip.vue';

// 导入所有特定类型的字段组件
// 注意：为了避免循环依赖，ObjectField, ArrayField, DictionaryField 内部如果需要
// 再次渲染 SchemaDrivenForm (或其部分功能)，需要小心处理。
// 更好的做法是它们内部直接使用其他简单类型组件或复杂类型组件。
import StringInputControl from './fields/StringInputControl.vue';
import NumberInputControl from './fields/NumberInputControl.vue';
import BooleanSwitchControl from './fields/BooleanSwitchControl.vue'; // 待创建
import EnumSelectControl from './fields/EnumSelectControl.vue';
import DateTimeControl from './fields/DateTimeControl.vue';     // 待创建
import ObjectField from './fields/ObjectField.vue';           // 待创建
import ArrayField from './fields/ArrayField.vue';             // 待创建
import DictionaryField from './fields/DictionaryField.vue';       // 待创建
import UnknownField from './fields/UnknownField.vue';

const props = defineProps({
  schema: {
    type: Array as PropType<FormFieldSchema[]>,
    required: true,
  },
  modelValue: {
    type: Object as PropType<Record<string, any>>,
    required: true,
  },
  disabled: {
    type: Boolean,
    default: false,
  },
  basePath: {
    type: String,
    default: '',
  },
  labelWidth: {
    type: [String, Number] as PropType<string | number | undefined>,
    default: 'auto',
  },
  nestedLabelWidth: {
    type: [String, Number] as PropType<string | number | undefined>,
    default: 80,
  },
  labelAlign: {
    type: String as PropType<'left' | 'right' | undefined>,
    default: 'left',
  },
  labelPlacement: {
    type: String as PropType<'left' | 'top'>,
    default: 'top',
  },
  nestedLabelPlacement: {
    type: String as PropType<'left' | 'top' | undefined>,
    default: undefined,
  },
});

const emit = defineEmits(['update:modelValue']);

const formRef = ref<FormInst | null>(null);
const internalModel = ref<Record<string, any>>({});
const generatedRules = ref<FormRules>({});

const sortedSchema = computed(() => {
  return [...props.schema].sort((a, b) => (a.order || 0) - (b.order || 0));
});

function getFieldPath(fieldName: string): string {
  return props.basePath ? `${props.basePath}.${fieldName}` : fieldName;
}

// 解析组件
function resolveFieldComponent(fieldSchema: FormFieldSchema): VueComponent | string {
  switch (fieldSchema.schemaDataType) {
    case SchemaDataType.STRING:
    case SchemaDataType.MULTILINE_TEXT:
    case SchemaDataType.PASSWORD:
    case SchemaDataType.GUID:
      // 如果 STRING 类型配置了 options 且 isEditableSelectOptions, 也应使用 EnumSelectControl
      if (fieldSchema.schemaDataType === SchemaDataType.STRING &&
          fieldSchema.options && fieldSchema.options.length > 0 &&
          fieldSchema.isEditableSelectOptions) {
        return EnumSelectControl;
      }
      return StringInputControl;
    case SchemaDataType.NUMBER:
    case SchemaDataType.INTEGER:
      return NumberInputControl;
    case SchemaDataType.BOOLEAN:
      return BooleanSwitchControl; // 需要创建 BooleanSwitchControl.vue
    case SchemaDataType.ENUM:
      return EnumSelectControl;
    case SchemaDataType.DATE_TIME:
      return DateTimeControl; // 需要创建 DateTimeControl.vue
    case SchemaDataType.OBJECT:
      return ObjectField;       // 需要创建 ObjectField.vue
    case SchemaDataType.ARRAY:
      return ArrayField;        // 需要创建 ArrayField.vue
    case SchemaDataType.DICTIONARY:
      return DictionaryField;   // 需要创建 DictionaryField.vue
    default:
      // 返回一个能显示错误信息的组件，并传递必要信息
      return UnknownField; // UnknownField.vue 会显示错误
  }
}

// 深拷贝函数 (保持原样或使用更健壮的库)
function deepClone<T>(obj: T): T {
  if (obj === null || typeof obj !== 'object') {
    return obj;
  }
  if (obj instanceof Date) {
    return new Date(obj.getTime()) as any;
  }
  if (Array.isArray(obj)) {
    const clonedArray = obj.map(item => deepClone(item));
    return clonedArray as any;
  }
  const clonedObj = {} as T;
  for (const key in obj) {
    if (Object.prototype.hasOwnProperty.call(obj, key)) {
      clonedObj[key] = deepClone(obj[key]);
    }
  }
  return clonedObj;
}


// 默认值创建 (每个字段组件可以自己处理 defaultValue，但顶层模型初始化可能仍需要)
// 这个函数现在主要用于初始化 internalModel 的骨架
function createDefaultValueForField(field: FormFieldSchema): any {
  if (field.defaultValue !== undefined && field.defaultValue !== null) {
    return deepClone(field.defaultValue);
  }
  switch (field.schemaDataType) {
    case SchemaDataType.STRING:
    case SchemaDataType.MULTILINE_TEXT:
    case SchemaDataType.PASSWORD:
    case SchemaDataType.GUID:
      return '';
    case SchemaDataType.NUMBER:
    case SchemaDataType.INTEGER:
      return null;
    case SchemaDataType.BOOLEAN:
      return false;
    case SchemaDataType.ENUM:
    case SchemaDataType.DATE_TIME:
      return null;
    case SchemaDataType.OBJECT:
      if (field.nestedSchema) {
        const obj: Record<string, any> = {};
        field.nestedSchema.forEach(nestedField => {
          obj[nestedField.name] = createDefaultValueForField(nestedField);
        });
        return obj;
      }
      return {};
    case SchemaDataType.ARRAY:
      return []; // ArrayField 内部会处理新项的创建
    case SchemaDataType.DICTIONARY:
      return {}; // DictionaryField 内部会处理数据结构 (可能是 Record 或 Array of pairs)
                 // 如果 DictionaryField emit 的是 Record，这里是 {} 就对了
                 // 如果它 emit Array of pairs，这里应该是 []
                 // 假设 DictionaryField emit Record<string, any>
    default:
      return null;
  }
}

// 初始化/更新内部模型
watch(() => props.modelValue, (newModel) => {
  const modelChanged = JSON.stringify(internalModel.value) !== JSON.stringify(newModel);
  if (modelChanged) {
    const tempInternalModel: Record<string, any> = {};
    props.schema.forEach(field => {
      // 优先使用 modelValue 中的值，否则使用 schema 定义的默认值
      if (newModel && newModel[field.name] !== undefined) {
        tempInternalModel[field.name] = deepClone(newModel[field.name]);
      } else {
        tempInternalModel[field.name] = createDefaultValueForField(field);
      }
    });
    internalModel.value = tempInternalModel;
  }
}, {immediate: true, deep: true});


// 监听 schema 变化
watch(() => props.schema, (newSchema, oldSchema) => {
  if (JSON.stringify(newSchema) === JSON.stringify(oldSchema)) return;

  const tempModel = {...internalModel.value};
  const newInternalModel: Record<string, any> = {};
  newSchema.forEach(field => {
    if (tempModel[field.name] !== undefined) {
      newInternalModel[field.name] = deepClone(tempModel[field.name]); // 保留已有值
    } else {
      // 为新字段或 schema 中现有但 modelValue 中没有的字段设置默认值
      newInternalModel[field.name] = createDefaultValueForField(field);
    }
  });
  internalModel.value = newInternalModel;
  regenerateAllRules();
}, {immediate: true, deep: true});


// 字段值更新时，发出 update:modelValue 事件
function handleFieldValueUpdate(fieldName: string, value: any) {
  // internalModel.value[fieldName] = value; // 子组件 v-model 已经更新了

  // 创建一个新的输出模型副本，基于 props.modelValue 以保留不在当前 schema 中的字段
  const outputModel = {...props.modelValue};

  // 将 internalModel 的值（仅限当前 schema 中的字段）合并到 outputModel
  // 这里的 internalModel 已经是各个子组件emit上来的正确格式的值
  props.schema.forEach(field => {
    if (internalModel.value.hasOwnProperty(field.name)) {
      outputModel[field.name] = internalModel.value[field.name];
    }
  });

  emit('update:modelValue', outputModel);
}


// 校验规则生成 (与之前类似，但要确保路径正确)
function mapValidationRules(field: FormFieldSchema): FormItemRule[] {
  const rules: FormItemRule[] = [];
  const validation = field.validation;
  const baseMessage = validation?.errorMessage || `${field.label}无效`;

  if (field.isRequired) {
    const requiredRule: FormItemRule = {
      required: true,
      message: validation?.errorMessage || `${field.label}是必填项`,
    };
    switch (field.schemaDataType) {
      case SchemaDataType.STRING:
      case SchemaDataType.MULTILINE_TEXT:
      case SchemaDataType.PASSWORD:
      case SchemaDataType.GUID:
        requiredRule.trigger = ['input', 'blur'];
        requiredRule.type = 'string';
        break;
      case SchemaDataType.NUMBER:
      case SchemaDataType.INTEGER:
        requiredRule.trigger = ['input', 'blur'];
        requiredRule.type = 'number';
        break;
      case SchemaDataType.ENUM:
      case SchemaDataType.DATE_TIME:
        requiredRule.trigger = ['change', 'blur'];
        if (field.schemaDataType === SchemaDataType.ENUM) {
          // Naive UI select 的值可能是 string 或 number
          // requiredRule.type = typeof createDefaultValueForField(field) === 'number' ? 'number' : 'string'; // 尝试推断
        }
        if (field.schemaDataType === SchemaDataType.DATE_TIME) requiredRule.type = 'number'; // Timestamp
        break;
      case SchemaDataType.BOOLEAN:
        requiredRule.trigger = ['change'];
        requiredRule.type = 'boolean';
        break;
      case SchemaDataType.ARRAY:
        requiredRule.trigger = ['change']; // ArrayField 内部可能需要更细致的校验触发
        requiredRule.type = 'array';
        requiredRule.validator = (rule, value) => value && Array.isArray(value) && value.length > 0;
        break;
      case SchemaDataType.DICTIONARY:
        requiredRule.trigger = ['change'];
        requiredRule.type = 'object'; // DictionaryField emit 的是 Record<string,any>
        requiredRule.validator = (rule, value) => value && typeof value === 'object' && Object.keys(value).length > 0;
        break;
      default:
        requiredRule.trigger = ['input', 'blur'];
    }
    rules.push(requiredRule);
  }

  if (validation) {
    const otherRule: FormItemRule = {trigger: ['input', 'blur']};
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
    if (validation.minLength !== undefined && validation.minLength !== null &&
        (field.schemaDataType === SchemaDataType.STRING || field.schemaDataType === SchemaDataType.MULTILINE_TEXT || field.schemaDataType === SchemaDataType.PASSWORD || field.schemaDataType === SchemaDataType.GUID)
    ) {
      otherRule.min = validation.minLength; // n-input 的 minlength 是属性，FormItemRule 的 min 是针对长度
      otherRule.type = 'string';
      hasOtherValidation = true;
    }
    if (validation.maxLength !== undefined && validation.maxLength !== null &&
        (field.schemaDataType === SchemaDataType.STRING || field.schemaDataType === SchemaDataType.MULTILINE_TEXT || field.schemaDataType === SchemaDataType.PASSWORD || field.schemaDataType === SchemaDataType.GUID)
    ) {
      otherRule.max = validation.maxLength; // n-input 的 maxlength 是属性，FormItemRule 的 max 是针对长度
      otherRule.type = 'string';
      hasOtherValidation = true;
    }
    if (validation.pattern) {
      if (validation.pattern.toLowerCase() === 'url') {
        rules.push({type: 'url', message: baseMessage, trigger: ['input', 'blur']});
      } else {
        try {
          otherRule.pattern = new RegExp(validation.pattern);
          otherRule.type = 'string';
          hasOtherValidation = true;
        } catch (e) {
          console.warn(`[SchemaDrivenForm] 无效的正则表达式 for ${field.name}: ${validation.pattern}`, e);
        }
      }
    }
    if (hasOtherValidation) {
      otherRule.message = baseMessage;
      if (otherRule.type === 'number') otherRule.trigger = ['input', 'blur'];
      rules.push(otherRule);
    }
  }
  return rules;
}

function regenerateAllRules() {
  const newRules: FormRules = {};
  props.schema.forEach(field => {
    const pathKey = getFieldPath(field.name);
    newRules[pathKey] = mapValidationRules(field);

    // 如果是复杂类型，其内部字段的规则由其各自的 SchemaDrivenForm (或等效逻辑) 管理。
    // 但顶层的 Array/Object/Dictionary 本身也可能有规则 (如 isRequired)。
  });
  generatedRules.value = newRules;
}


function handleFieldBlur(path: string | undefined) {
  if (path && formRef.value) {
    // 尝试校验单个字段，但 Naive UI 的 validate 可能不直接支持按 path 字符串精确匹配
    // formRef.value.validate(path_or_paths_array).catch(() => {});
    // 或者，依赖于 blur 时的 model update 触发的校验
  }
}

// 暴露方法
defineExpose({
  validate: () => formRef.value?.validate(),
  restoreValidation: () => formRef.value?.restoreValidation(),
  resetFields: () => {
    const initialModel: Record<string, any> = {};
    props.schema.forEach(field => {
      if (props.modelValue && props.modelValue[field.name] !== undefined) {
        initialModel[field.name] = deepClone(props.modelValue[field.name]);
      } else {
        initialModel[field.name] = createDefaultValueForField(field);
      }
    });
    internalModel.value = initialModel; // 这会通过 watch 更新到子组件
    // 触发更新到父组件
    emit('update:modelValue', deepClone(initialModel));

    nextTick(() => {
      formRef.value?.restoreValidation();
    });
  }
});

</script>

<style scoped>
.nested-form-container { /* 这个样式现在应该在 ObjectField.vue 中定义 */
  width: 100%;
  padding: 10px;
  border: 1px solid #eee;
  border-radius: 3px;
  margin-top: 5px;
}
</style>