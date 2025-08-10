<template>
  <div class="editable-variable-tag">
    <!-- Popover 保持在最外层，包裹住交互的 Tag -->
    <n-popover :disabled="isEditing || !spec" placement="top-start" trigger="hover">
      <template #trigger>
        <n-tag
            :closable="closable && !isEditing"
            :type="tagType"
            @dblclick="startEditing"
            @close.stop.prevent="onRemove"
        >
          <!-- 核心改动：在 n-tag 内部切换编辑和显示状态 -->
          <n-auto-complete
              v-if="isEditing"
              ref="inputRef"
              v-model:value="editingValue"
              :options="availableOptions"
              class="seamless-editor"
              clear-after-select
              placeholder="输入变量名"
              size="small"
              @blur="handleBlur"
              @select="handleSelect"
              @keydown.enter.prevent="handleEnter"
          />
          <span v-else class="tag-text">
            <span v-if="name">{{ name }}</span>
            <span v-else style="color: #aaa;">未命名</span>
          </span>
        </n-tag>
      </template>

      <!-- Popover 的内容：显示详细的变量定义 -->
      <n-descriptions v-if="spec" :column="1" bordered label-placement="top" size="small" title="变量详情">
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
import {computed, nextTick, ref, watch} from 'vue';
import {NAutoComplete, NDescriptions, NDescriptionsItem, NPopover, NTag} from 'naive-ui';
import type {ConsumedSpec, ProducedSpec} from "@/app-workbench/types/generated/workflow-config-api-client";

type VarSpec = ConsumedSpec | ProducedSpec;

const props = withDefaults(defineProps<{
  name?: string;
  spec?: VarSpec | null;
  closable?: boolean;
  availableOptions?: Array<{ label: string, value: string }>;
}>(), {
  name: '',
  spec: null,
  closable: false,
  availableOptions: () => [],
});

// const props = defineProps({
//   name: {
//     type: String,
//     required: true
//   },
//   spec: {
//     type: Object as PropType<VarSpec>,
//     default: null,
//   },
//   closable: {
//     type: Boolean,
//     default: false,
//   },
//   availableOptions: {
//     type: Array as PropType<Array<{ label: string, value: string }>>,
//     default: () => []
//   }
// });

const emit = defineEmits(['update:name', 'remove', 'create']);

const isEditing = ref(false);
const editingValue = ref(props.name); // 使用一个独立的 ref 来绑定输入框的值
const inputRef = ref<InstanceType<typeof NAutoComplete> | null>(null);

watch(() => props.name, (newName) =>
{
  editingValue.value = newName;
});

async function startEditing()
{
  // 不允许编辑一个空的 closable 标签（通常是“添加”按钮）
  if (props.closable && !props.name) return;

  // 如果已经在编辑，则不执行任何操作
  if (isEditing.value) return;

  editingValue.value = props.name; // 同步当前值为编辑值
  isEditing.value = true;
  await nextTick();
  inputRef.value?.focus();
}

function finishEditing(newValue?: string)
{
  if (!isEditing.value) return;

  const finalValue = (newValue ?? editingValue.value).trim();

  // 如果初始 name 为空，且有了新值，则认为是创建
  if (!props.name && finalValue)
  {
    emit('create', finalValue);
  }
  // 如果初始 name 不为空，且值发生了变化，则认为是更新
  else if (props.name && finalValue && finalValue !== props.name)
  {
    emit('update:name', finalValue);
  }

  isEditing.value = false;
}


function handleSelect(value: string)
{
  finishEditing(value);
}

function handleEnter()
{
  finishEditing();
}

function handleBlur()
{
  finishEditing();
}

function onRemove()
{
  emit('remove');
}

const tagType = computed(() =>
{
  if (props.spec && 'isOptional' in props.spec && !props.spec.isOptional)
  {
    return 'error';
  }
  return props.spec ? 'success' : 'default';
});

defineExpose({
  startEditing
});
</script>

<style scoped>
.editable-variable-tag {
  display: inline-flex; /* 使用 flex 确保内部元素正确对齐 */
  vertical-align: middle; /* 确保标签和其他文本对齐 */
}

.n-tag {
  cursor: pointer;
  /* 确保标签在编辑时有足够的空间容纳输入框 */
  min-width: 50px;
  /* 当 tag 内容是 input 时，需要 flex 布局让 input 撑开 */
  display: inline-flex;
  align-items: center;
}

.tag-text {
  /* 确保文本和输入框垂直对齐 */
  line-height: 1;
}

</style>