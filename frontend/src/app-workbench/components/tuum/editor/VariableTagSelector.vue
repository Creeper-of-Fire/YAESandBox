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
        @create="newValue => handleCreate(index, newValue)"
    />

    <!-- 2. 一个“添加”按钮 -->
    <n-button v-if="allowAdd" size="small" dashed @click="handleAddNewTag">
      {{ placeholder }}
    </n-button>
  </div>
</template>

<script lang="ts" setup>
import {computed, nextTick, onBeforeUpdate, type PropType, ref} from 'vue';
import VariableTag from './VariableTag.vue';
import type {ConsumedSpec, ProducedSpec} from '@/app-workbench/types/generated/workflow-config-api-client';
// 定义 VariableTag 组件实例的类型
type VariableTagInstance = InstanceType<typeof VariableTag>;

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

// 用于存储所有 VariableTag 子组件实例的引用
const tagRefs = ref<Record<number, VariableTagInstance>>({});

// 在 v-for 中设置 ref 的标准做法
const setTagRef = (el: any, index: number) => {
  if (el) {
    tagRefs.value[index] = el;
  }
};

// 在组件更新前清空 refs，防止内存泄漏和引用错误
onBeforeUpdate(() => {
  tagRefs.value = {};
});

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

// 处理添加新标签的动作
async function handleAddNewTag() {
  // 1. 在 modelValue 的副本中添加一个空字符串作为占位符
  const newVars = [...props.modelValue, ''];
  emit('update:modelValue', newVars);

  // 2. 等待 DOM 更新，新的 VariableTag 组件会被渲染出来
  await nextTick();

  // 3. 获取刚刚创建的新标签的 ref
  const newTagIndex = newVars.length - 1;
  const newTagRef = tagRefs.value[newTagIndex];

  // 4. 调用子组件暴露的 startEditing 方法，使其进入编辑状态
  if (newTagRef) {
    await newTagRef.startEditing();
  }
}

// 计算某个已存在的标签在编辑时可选的列表（包含它自己）
function getAvailableOptionsForTag(currentName: string)
{
  const selectedSet = new Set(selectedVars.value);
  return props.availableSpecs
      .filter(spec => !selectedSet.has(spec.name) || spec.name === currentName)
      .map(spec => ({label: spec.name, value: spec.name}));
}

// 当一个由空字符串占位符创建的标签完成编辑时触发
function handleCreate(indexToUpdate: number, newName: string) {
  if (!newName) {
    // 如果用户没有输入任何内容就取消了，直接移除这个占位符
    handleRemove(indexToUpdate);
    return;
  }

  const newVars = [...props.modelValue];
  // 检查新名称是否已存在（不包括当前位置，以防万一）
  const isDuplicate = newVars.some((v, i) => i !== indexToUpdate && v === newName);

  if (!isDuplicate) {
    newVars[indexToUpdate] = newName;
    emit('update:modelValue', newVars);
  } else {
    // 如果名称重复，则不作改变，直接移除占位符
    handleRemove(indexToUpdate);
  }
}

function handleUpdate(indexToUpdate: number, newName: string) {
  const newVars = [...props.modelValue];
  // 检查新名称是否已存在（不包括当前位置）
  const isDuplicate = newVars.some((v, i) => i !== indexToUpdate && v === newName);

  if (newName && !isDuplicate) {
    newVars[indexToUpdate] = newName;
    emit('update:modelValue', newVars);
  } else {
    // 如果新名称无效或已存在，这里我们选择移除
    handleRemove(indexToUpdate);
  }
}

function handleRemove(indexToRemove: number) {
  const newVars = [...props.modelValue];
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