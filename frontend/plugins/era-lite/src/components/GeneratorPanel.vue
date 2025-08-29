<template>
  <n-card :title="props.title">
    <n-flex :size="12" vertical>
      <!-- 用户输入区域 -->
      <n-input
          v-model:value="generationTopic"
          :placeholder="props.generationPromptLabel"
          clearable
          type="text"
      />

      <!-- 配置提供者按钮 -->
      <WorkflowProviderButton
          :expected-inputs="props.expectedInputs"
          :storage-key="props.storageKey"
          @click="handleGenerate"
      />
    </n-flex>

    <!-- AI生成结果展示区域-->
    <div v-if="isLoading || modelValue" style="margin-top: 24px;">
      <n-divider/>
      <n-alert v-if="error" title="生成失败" type="error">
        {{ error.message }}
      </n-alert>
      <div v-else-if="modelValue">
        <!-- 根据 Schema 动态渲染字段 -->
        <n-thing v-for="field in props.schema" :key="field.key">
          <template #header>
            <n-h3 style="margin: 0">{{ field.label }}</n-h3>
          </template>
          <template #description>
            <p style="white-space: pre-wrap;">
              {{ (modelValue as any)[field.key] ?? '...' }}
            </p>
          </template>
        </n-thing>

        <!-- AI 思考过程展示 -->
        <n-collapse-transition :show="!!thinkingProcess">
          <n-log :log="thinkingProcess" language="text" style="margin-top: 16px;" trim/>
        </n-collapse-transition>
      </div>

      <n-flex v-if="isFinished" justify="end" style="margin-top: 16px;">
        <n-button :disabled="!isDataComplete" type="primary" @click="handleAccept">
          直接添加
        </n-button>
        <n-button @click="handleEditAndAccept">
          {{ isDataComplete ? '编辑后添加' : '补全并添加' }}
        </n-button>
        <n-button @click="handleClear">丢弃</n-button>
      </n-flex>
    </div>

    <EntityEditor
        v-if="modelValue"
        v-model:show="showEditor"
        :entity-name="entityName"
        :initial-data="modelValue"
        :schema="schema"
        mode="complete"
        @save="handleSaveFromEditor"
    />
  </n-card>
</template>

<script generic="T extends Record<string, any>" lang="ts" setup>
import {computed, type Ref, ref, watch} from 'vue';
import {useMessage} from 'naive-ui';
import type {WorkflowConfig} from '@yaesandbox-frontend/core-services/types';
import {useStructuredWorkflowStream} from '#/composables/useStructuredWorkflowStream';
import WorkflowProviderButton from "#/components/WorkflowProviderButton.vue";
import EntityEditor from "#/components/EntityEditor.vue";
import type {EntityFieldSchema} from "#/types/entitySchema.ts";

const props = defineProps<{
  modelValue: Partial<T> | null;
  schema: EntityFieldSchema[];
  storageKey: string;
  expectedInputs: string[];
  title: string;
  generationPromptLabel: string;
  entityName: string;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: Partial<T> | null): void;
  (e: 'accept', value: Omit<T, 'id'>): void;
}>();

const message = useMessage();
const generationTopic = ref('');

// 将 v-model 转换为内部可写的 ref
const internalModel = ref(props.modelValue) as Ref<Partial<T> | null>;
watch(() => props.modelValue, (newVal) =>
{
  internalModel.value = newVal;
});
watch(internalModel, (newVal) =>
{
  emit('update:modelValue', newVal);
});


const {
  isLoading,
  error,
  isFinished,
  execute,
  thinkingProcess,
  clear,
} = useStructuredWorkflowStream<Partial<T>>(internalModel, props.schema);

// --- 校验和编辑器逻辑 ---
// 数据完整性校验
const isDataComplete = computed(() =>
{
  if (!internalModel.value) return false;
  // 遍历 schema，检查每一项在 internalModel 中是否存在且不为空
  return props.schema.every(field =>
  {
    const value = (internalModel.value as any)[field.key];
    return value !== null && value !== undefined && value !== '';
  });
});

// 控制外部编辑器的显示状态
const showEditor = ref(false);


async function handleGenerate(config: WorkflowConfig)
{
  if (!generationTopic.value.trim())
  {
    message.warning('请输入生成内容的核心描述！');
    return;
  }
  const inputs = {topic: generationTopic.value};
  await execute(config, inputs);
}

// “直接添加”按钮的处理
function handleAccept()
{
  if (internalModel.value)
  {
    if (isDataComplete.value)
    {
      // 显式地移除 id 属性（如果存在），以符合 Omit<T, 'id'> 类型
      const {id, ...dataToAccept}: Partial<T> = internalModel.value;
      emit('accept', dataToAccept as Omit<T, 'id'>);
      clear();
    }
    else
    {
      // 理论上 disabled 状态会阻止这个分支，但作为保险
      message.warning('数据不完整，请先补全。');
    }
  }
}

function handleClear()
{
  clear();
}

// "编辑/补全后添加" 按钮
function handleEditAndAccept()
{
  if (internalModel.value)
  {
    showEditor.value = true;
  }
}

// 监听内部编辑器的 save 事件
function handleSaveFromEditor(completedData: Omit<T, 'id'>)
{
  // 收到编辑器保存的数据后，通过 accept 事件将最终结果向上冒泡
  emit('accept', completedData);
  // 清理自身状态，准备下一次生成
  clear();
}
</script>