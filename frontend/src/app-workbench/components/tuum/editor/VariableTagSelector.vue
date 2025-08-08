<!-- src/app-workbench/components/.../VariableTagSelector.vue (新建) -->
<template>
  <div class="variable-tag-selector">
    <!-- 1. 循环渲染已有的、可编辑的 VariableTag -->
    <VariableTag
        v-for="(varName, index) in selectedVars"
        :key="`${varName}-${index}`"
        :available-options="getAvailableOptionsForTag(varName)"
        :name="varName"
        :spec="specMap.get(varName)"
        closable
        @remove="handleRemove(index)"
        @update:name="newValue => handleUpdate(index, newValue)"
    />

    <!-- 2. 用于添加新 Tag 的独立 AutoComplete -->
    <n-auto-complete
        v-model:value="inputValue"
        :get-show="() => availableOptionsForNewTag.length > 0"
        :options="availableOptionsForNewTag"
        :placeholder="placeholder"
        class="tag-input"
        clearable
        size="small"
        @select="handleAdd"
    />
  </div>
</template>

<script lang="ts" setup>
import {computed, type PropType, ref} from 'vue';
import {NAutoComplete} from 'naive-ui';
import VariableTag from './VariableTag.vue';
import type {ConsumedSpec, ProducedSpec} from '@/app-workbench/types/generated/workflow-config-api-client';

type VarSpec = ConsumedSpec | ProducedSpec;

const props = defineProps({
  modelValue: {
    type: Array as PropType<string[]>,
    required: true,
  },
  // spec 定义是可选的。如果没有提供，则无法进行类型提示和补全。
  availableSpecs: {
    type: Array as PropType<VarSpec[]>,
    default: () => [],
  },
  placeholder: {
    type: String,
    default: '添加'
  },
  // 控制是否允许添加新标签。如果为 false，则组件为只读模式
  allowAdd: {
    type: Boolean,
    default: true,
  },
  // 控制是否只允许从列表中选择
  restrictToList: {
    type: Boolean,
    default: false
  },
});

const emit = defineEmits(['update:modelValue']);

// 内部状态，用于绑定 AutoComplete 输入框
const inputValue = ref('');

// 使用 v-model 的标准计算属性模板
const selectedVars = computed({
  get: () => props.modelValue,
  set: (newVal) => emit('update:modelValue', newVal),
});

// 为了高效查找，将传入的 spec 数组转换为 Map
const specMap = computed(() =>
{
  const map = new Map<string, VarSpec>();
  props.availableSpecs.forEach(spec => map.set(spec.name, spec));
  return map;
});

// 可用于添加新标签的选项（过滤掉已选的）
const availableOptionsForNewTag = computed(() =>
{
  const selectedSet = new Set(selectedVars.value);
  return props.availableSpecs
      .filter(spec => !selectedSet.has(spec.name))
      .map(spec => ({label: spec.name, value: spec.name}));
});

// 计算某个已存在的标签在编辑时可选的列表（包含它自己）
function getAvailableOptionsForTag(currentName: string)
{
  const selectedSet = new Set(selectedVars.value);
  return props.availableSpecs
      .filter(spec => !selectedSet.has(spec.name) || spec.name === currentName)
      .map(spec => ({label: spec.name, value: spec.name}));
}

function handleAdd(name: string)
{
  if (name && !selectedVars.value.includes(name))
  {
    selectedVars.value = [...selectedVars.value, name];
  }
  // 清空输入框
  inputValue.value = '';
}

function handleUpdate(indexToUpdate: number, newName: string)
{
  const newVars = [...selectedVars.value];
  newVars[indexToUpdate] = newName;
  emit('update:modelValue', newVars);
}

function handleRemove(indexToRemove: number)
{
  const newVars = [...selectedVars.value];
  newVars.splice(indexToRemove, 1);
  emit('update:modelValue', newVars);
}
</script>

<style scoped>
.variable-tag-selector {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  flex-grow: 1;
}

.tag-input {
  max-width: 150px;
}
</style>