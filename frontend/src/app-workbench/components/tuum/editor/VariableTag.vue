<!-- src/app-workbench/components/.../VariableTag.vue -->
<template>
  <div class="editable-variable-tag">
    <n-auto-complete
        v-if="isEditing"
        ref="inputRef"
        :options="availableOptions"
        :value="name"
        placeholder="输入变量名"
        size="small"
        @blur="handleBlur"
        @select="handleSelect"
        @keyup.enter="handleEnter"
    />
    <n-popover placement="top-start" trigger="hover">
      <template #trigger>
        <n-tag
            :closable="closable"
            :type="tagType"
            @dblclick="startEditing"
            @close.stop.prevent="onRemove"
        >
          <span v-if="name">{{ name }}</span>
          <span v-else style="color: #aaa;">未命名</span>
        </n-tag>
      </template>
      <!-- Popover 的内容：显示详细的变量定义 -->
      <n-descriptions :column="1" bordered label-placement="top" size="small" title="变量详情">
        <n-descriptions-item label="变量名">
          {{ spec.name }}
        </n-descriptions-item>
        <n-descriptions-item label="类型">
          <n-tag :bordered="false" size="small" type="info">{{ spec.def.typeName }}</n-tag>
        </n-descriptions-item>
        <n-descriptions-item v-if="spec.def.description" label="描述">
          {{ spec.def.description }}
        </n-descriptions-item>
        <n-descriptions-item v-if="'isOptional' in spec" label="是否必需">
          {{ !spec.isOptional ? '是 (必须提供来源)' : '否 (内部已满足或可选)' }}
        </n-descriptions-item>
      </n-descriptions>
    </n-popover>
  </div>
</template>

<script lang="ts" setup>
import {computed, nextTick, type PropType, ref, watch} from 'vue';
import {type NAutoComplete, NDescriptions, NDescriptionsItem, NPopover, NTag} from 'naive-ui';
import type {ConsumedSpec, ProducedSpec} from "@/app-workbench/types/generated/workflow-config-api-client";

type VarSpec = ConsumedSpec | ProducedSpec;

const props = defineProps({
  name: {
    type: String,
    required: true
  },
  // spec 是可选的，用于提供丰富的类型提示
  spec: {
    type: Object as PropType<VarSpec>,
    default: null,
  },
  closable: {
    type: Boolean,
    default: false,
  },
  // 接收可用的补全选项
  availableOptions: {
    type: Array as PropType<Array<{ label: string, value: string }>>,
    default: () => []
  },
  startInEditMode: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits(['update:name', 'remove', 'create']);

const isEditing = ref(false);
const inputRef = ref<InstanceType<typeof NAutoComplete> | null>(null);

// 监视 startInEditMode prop，一旦为 true 就进入编辑状态
watch(() => props.startInEditMode, (isNew) => {
  if (isNew) {
    startEditing();
  }
}, { immediate: true });

async function startEditing()
{
  // 不允许编辑一个空的 closable 标签，这是选择器的添加按钮
  if (props.closable && !props.name) return;
  isEditing.value = true;
  // DOM 更新后，自动聚焦到输入框
  await nextTick();
  inputRef.value?.focus();
}

function finishEditing(newValue?: string) {
  const finalValue = newValue ?? props.name;

  // 如果是新建（原来的 name 是空的）并且有新值
  if (props.startInEditMode && finalValue) {
    emit('create', finalValue);
  }
  // 如果是编辑（原来的 name 不是空的）且值有变化
  else if (!props.startInEditMode && finalValue !== props.name) {
    emit('update:name', finalValue);
  }

  isEditing.value = false;
}

function handleSelect(value: string) {
  finishEditing(value);
}

function handleEnter(e: KeyboardEvent) {
  finishEditing((e.target as HTMLInputElement).value);
}

function handleBlur() {
  finishEditing();
}

function onRemove() {
  emit('remove');
}

const tagType = computed(() =>
{
  // 如果 spec 存在且是必需的，则为 error，否则为 success 或 default
  if (props.spec && 'isOptional' in props.spec && !props.spec.isOptional)
  {
    return 'error';
  }
  return props.spec ? 'success' : 'default'; // 有 spec 但非必需，或 spec 不存在
});
</script>

<style scoped>
.editable-variable-tag {
  display: inline-block;
}
.n-tag {
  cursor: pointer; /* 提示用户可以交互 */
}
</style>