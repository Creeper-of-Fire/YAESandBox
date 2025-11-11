<!-- GeneratorPanel.vue -->
<template>
  <n-card :title="props.title">
    <n-flex :size="12" vertical>
      <!-- 用户输入区域 -->
      <n-input
          v-model:value="generationTopic"
          :placeholder="props.generationPromptLabel"
          clearable
          type="textarea"
      />

      <n-flex :size="8" align="center" style="margin-top: -4px; margin-bottom: 4px;">
        <n-text :depth="3" style="font-size: 12px;">
          固定输入参数:
        </n-text>
        <n-code inline style="font-size: 12px; padding: 2px 6px;">
          entityName
        </n-code>
        <n-text :depth="3" style="font-size: 12px;">=</n-text>
        <n-tag :bordered="false" round size="small" type="info">
          {{ props.entityName }}
        </n-tag>
      </n-flex>

      <!-- 配置提供者按钮 -->
      <WorkflowSelectorButton
          :expected-outputs="expectedOutputsFromSchema"
          :filter="workflowFilter"
          :storage-key="props.storageKey"
          @click="handleGenerate"/>
    </n-flex>

    <!-- AI生成结果展示区域-->
    <div v-if="isLoading || modelValue" style="margin-top: 24px;">
      <n-divider/>
      <n-alert v-if="error" title="生成失败" type="error">
        {{ error.message }}
      </n-alert>
      <div v-else-if="modelValue">
        <!-- 根据 Schema 动态渲染字段 -->
        <n-thing v-for="field in props.schema" :key="getKey(field)">
          <template #header>
            <n-h3 style="margin: 0">{{ field.label }}</n-h3>
          </template>
          <template #description>
            <p style="white-space: pre-wrap;">
              {{ (modelValue as any)[getKey(field)] ?? '...' }}
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
  </n-card>
</template>

<script generic="T extends Record<string, any>" lang="ts" setup>
import {computed, type Ref, ref, watch} from 'vue';
import {useMessage} from 'naive-ui';
import type {WorkflowConfig} from '@yaesandbox-frontend/core-services/types';
import {
  type EntityFieldSchema,
  getKey,
  useFlatDataWithSchema,
  useStructuredWorkflowStream,
  type WorkflowFilter
} from '@yaesandbox-frontend/core-services/composables';
import {extractOutputsFromSchema, WorkflowSelectorButton} from '@yaesandbox-frontend/core-services/workflow'
import {useVModel} from "@vueuse/core";
import {useEntityEditorModal} from "#/components/useEntityEditorModal.tsx";

const props = defineProps<{
  modelValue: Partial<T> | null;
  schema: EntityFieldSchema[];
  storageKey: string;
  expectedInputs: string[];
  requiredTags?: string[];
  title: string;
  generationPromptLabel: string;
  entityName: string;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: Partial<T> | null): void;
  (e: 'accept', value: Omit<T, 'id'>): void;
}>();

const workflowFilter = ref<WorkflowFilter>({
  expectedInputs: [...props.expectedInputs, 'entityName'],
  requiredTags: props.requiredTags,
});

const message = useMessage();
const generationTopic = ref('');
// 控制外部编辑器的显示状态
const showEditor = ref(false);

const internalModel = useVModel(props, 'modelValue', emit) as Ref<Partial<T> | null>;

const {
  flatTextData,
  isLoading,
  error,
  isFinished,
  execute,
  thinkingProcess,
  clear: clearStream,
} = useStructuredWorkflowStream();

const {typedData: flatData} = useFlatDataWithSchema(flatTextData, props.schema)

const expectedOutputsFromSchema = computed(() => extractOutputsFromSchema(props.schema));

if (flatData)
{
  watch(flatData, (newData) =>
  {
    if (newData)
    {
      // 直接赋值给 useVModel 返回的 ref，它会自动 emit update 事件
      internalModel.value = newData as Partial<T>;
    }
  });
}

// --- 校验逻辑 ---
// 数据完整性校验
const isDataComplete = computed(() =>
{
  if (!internalModel.value) return false;
  // 遍历 schema，检查每一项在 internalModel 中是否存在且不为空
  return props.schema.every(field =>
  {
    const value = (internalModel.value as any)[getKey(field)];
    return value !== null && value !== undefined && value !== '';
  });
});


async function handleGenerate(config: WorkflowConfig)
{
  if (!generationTopic.value.trim())
  {
    message.warning('请输入生成内容的核心描述！');
    return;
  }
  const inputs = {
    topic: generationTopic.value,
    entityName: props.entityName
  };
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
      clearStream();
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
  clearStream();
  internalModel.value = null;
}

const entityEditor = useEntityEditorModal<T>();

function handleEditAndAccept() {
  if (!internalModel.value) return;
  entityEditor.open({
    mode: 'complete',
    entityName: props.entityName,
    schema: props.schema,
    initialData: internalModel.value,
    onSave: (completedData) => {
      emit('accept', completedData);
      clearStream();
    }
  });
}
</script>