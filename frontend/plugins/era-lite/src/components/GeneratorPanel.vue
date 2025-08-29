<template>
  <n-card :title="props.title">
    <n-flex :size="12" vertical>
      <!-- 用户输入区域 -->
      <n-input
          v-model:value="generationTopic"
          clearable
          :placeholder="props.generationPromptLabel"
          type="text"
      />

      <!-- 配置提供者按钮 -->
      <WorkflowProviderButton
          :storage-key="props.storageKey"
          :expected-inputs="props.expectedInputs"
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
              {{ field.type === 'number' && (modelValue as any)[field.key] ? ' G' : '' }}
            </p>
          </template>
        </n-thing>

        <!-- AI 思考过程展示 -->
        <n-collapse-transition :show="!!thinkingProcess">
          <n-log :log="thinkingProcess" language="text" trim style="margin-top: 16px;"/>
        </n-collapse-transition>
      </div>

      <n-flex v-if="isFinished" justify="end" style="margin-top: 16px;">
        <n-button type="primary" @click="handleAccept">添加到列表</n-button>
        <n-button @click="handleClear">丢弃</n-button>
      </n-flex>
    </div>
  </n-card>
</template>

<script setup lang="ts" generic="T extends object">
import {ref, type Ref, watch} from 'vue';
import {useMessage} from 'naive-ui';
import type {WorkflowConfig} from '@yaesandbox-frontend/core-services/types';
import {useStructuredWorkflowStream} from '#/composables/useStructuredWorkflowStream';
import type {SchemaField} from '#/types/generator';
import WorkflowProviderButton from "#/components/WorkflowProviderButton.vue";

const props = defineProps<{
  modelValue: T | null; // 使用 v-model 接收和更新结构化数据
  schema: SchemaField[]; // 动态渲染的 schema
  storageKey: string; // 用于 WorkflowProviderButton
  expectedInputs: string[]; // 用于 WorkflowProviderButton
  title: string;
  generationPromptLabel: string;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: T | null): void;
  (e: 'accept', value: T): void;
}>();

const message = useMessage();
const generationTopic = ref('');

// 将 v-model 转换为内部可写的 ref
const internalModel = ref(props.modelValue) as Ref<T | null>;
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
} = useStructuredWorkflowStream<T>(internalModel, props.schema);

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

function handleAccept()
{
  if (internalModel.value)
  {
    emit('accept', internalModel.value);
    clear(); // 清理状态
  }
}

function handleClear()
{
  clear();
}
</script>