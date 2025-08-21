<!-- src/app-workbench/components/tuum/editor/TuumMappingsEditor.vue -->
<template>
  <n-flex :size="16" vertical>
    <!-- 输入映射 -->
    <n-flex vertical>
      <!-- ✨ 智能提示与快捷操作区域 ✨ -->
      <n-alert
          v-if="analysisResult"
          :show-icon="true"
          :type="unmappedRequiredInputs.length > 0 ? 'warning' : 'success'"
      >
        <template v-if="unmappedRequiredInputs.length > 0">
          <n-popover :style="{ maxWidth: '400px' }" placement="bottom-start" trigger="hover">
            <template #trigger>
              <n-text strong style="cursor: help; border-bottom: 1px dashed;">
                发现 {{ unmappedRequiredInputs.length }} 个未映射的必需输入。
              </n-text>
            </template>
            <!-- Popover 内容：显示未映射的变量列表 -->
            <n-list :show-divider="false" size="small">
              <template #header>
                <n-text strong>以下必需变量需要被映射：</n-text>
              </template>
              <n-list-item v-for="spec in unmappedRequiredInputs" :key="spec.name">
                <n-flex justify="space-between">
                  <span>{{ spec.name }}</span>
                  <n-tag size="small" type="info">{{ spec.def.typeName }}</n-tag>
                </n-flex>
              </n-list-item>
            </n-list>
          </n-popover>
          <n-button
              style="margin-left: 12px; font-weight: bold;"
              text
              type="primary"
              @click="addAllRequiredInputs"
          >
            一键添加
          </n-button>
        </template>
        <template v-else>
          所有必需输入均已映射。
        </template>
      </n-alert>
      <MappingListEditor
          v-model:items="computedInputItems"
          :key-options="analysisResult?.internalConsumedSpecs ?? []"
          :value-options="[]"
          description="定义外部数据如何流入枢机内部。一个外部端点可以驱动多个内部变量。"
          key-header="内部变量 (消费者)"
          title="输入映射"
          value-header="外部端点 (提供者)"
      />
    </n-flex>

    <!-- 输出映射 -->
    <MappingListEditor
        v-model:items="computedOutputItems"
        :key-options="analysisResult?.internalProducedSpecs ?? []"
        :value-options="[]"
        description="定义内部变量如何作为枢机的输出。一个内部变量可以驱动多个外部端点。"
        key-header="内部变量 (提供者)"
        title="输出映射"
        value-header="外部端点 (输出名)"
    />
  </n-flex>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NFlex} from 'naive-ui';
import MappingListEditor from './MappingListEditor.vue';
import type {
  ConsumedSpec,
  TuumAnalysisResult,
  TuumInputMapping,
  TuumOutputMapping
} from "#/types/generated/workflow-config-api-client";

// --- 类型定义 ---
type InputMappings = Array<TuumInputMapping>;
type OutputMappings = Array<TuumOutputMapping>;

const props = defineProps<{
  analysisResult: TuumAnalysisResult | null;
  inputMappings: InputMappings;
  outputMappings: OutputMappings;
  availableGlobalVars?: string[];
}>();

const emit = defineEmits<{
  (e: 'update:input-mappings', value: InputMappings): void;
  (e: 'update:output-mappings', value: OutputMappings): void;
}>();

// --- ✨ 1. 计算出未映射的必需输入 ✨ ---
const unmappedRequiredInputs = computed<ConsumedSpec[]>(() =>
{
  if (!props.analysisResult)
  {
    return [];
  }
  // 获取所有已映射的内部变量的 key
  const mappedInternalVars = props.inputMappings.map(mapping => mapping.internalName);

  // 从分析结果中，筛选出那些“必需的” (IsOptional=false) 且“尚未被映射”的变量
  return props.analysisResult.internalConsumedSpecs.filter(spec =>
      !spec.isOptional && !mappedInternalVars.includes(spec.name)
  );
});

// --- ✨ 一键添加逻辑微调，以适配列表结构 ✨ ---
const addAllRequiredInputs = () =>
{
  if (unmappedRequiredInputs.value.length === 0) return;

  // 基于当前列表创建一个新数组副本
  const newMappings = [...props.inputMappings];

  unmappedRequiredInputs.value.forEach(spec =>
  {
    // 为每个未映射的必需输入创建一个新的 TuumInputMapping 对象
    newMappings.push({
      internalName: spec.name,
      endpointName: spec.name // 默认外部端点名与内部变量名相同
    });
  });

  // 发射事件，更新父组件的数据
  emit('update:input-mappings', newMappings);
};

const computedInputItems = computed({
  get()
  {
    return props.inputMappings
  },
  set(items)
  {
    emit('update:input-mappings', items);
  }
});

const computedOutputItems = computed({
  get()
  {
    return props.outputMappings
  },
  set(items)
  {
    emit('update:output-mappings', items);
  }
});
</script>