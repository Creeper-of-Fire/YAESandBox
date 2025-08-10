<!-- src/app-workbench/components/tuum/editor/TuumMappingsEditor.vue -->
<template>
  <n-space :size="16" vertical>
    <!-- 输入映射 -->
    <n-space vertical>
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
    </n-space>

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
  </n-space>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NSpace} from 'naive-ui';
import MappingListEditor from './MappingListEditor.vue';
import type {ConsumedSpec, TuumAnalysisResult} from "@/app-workbench/types/generated/workflow-config-api-client";

// --- 类型定义 ---
type InputMappings = Record<string, string>; // Dictionary<string, string>
type OutputMappings = Record<string, string[]>; // Dictionary<string, HashSet<string>>

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
  const mappedInternalVars = Object.keys(props.inputMappings);

  // 从分析结果中，筛选出那些“必需的” (IsOptional=false) 且“尚未被映射”的变量
  return props.analysisResult.internalConsumedSpecs.filter(spec =>
      !spec.isOptional && !mappedInternalVars.includes(spec.name)
  );
});

// --- ✨ 2. 实现一键添加的方法 ✨ ---
const addAllRequiredInputs = () =>
{
  if (unmappedRequiredInputs.value.length === 0) return;

  // 基于当前的映射创建一个新对象，以避免直接修改 props
  const newMappings = {...props.inputMappings};

  // 遍历所有未映射的必需输入，并将它们添加到新对象中
  unmappedRequiredInputs.value.forEach(spec =>
  {
    newMappings[spec.name] = spec.name;
  });

  // 发射事件，更新父组件的数据
  emit('update:input-mappings', newMappings);
};


// --- 输入映射转换 ---
// 使用 computed 的 get/set 实现双向数据转换
const computedInputItems = computed({
  // GET: 从 Dictionary<string, string> 转换为 Array<{key, value}>
  get()
  {
    return Object.entries(props.inputMappings).map(([key, value]) => ({
      key, // 内部变量
      value, // 外部端点
    }));
  },
  // SET: 从 Array<{key, value}> 转换回 Dictionary<string, string>
  set(items)
  {
    const newMappings: InputMappings = {};
    for (const item of items)
    {
      // 过滤掉 key 为空的无效映射
      newMappings[item.key] = item.value || '';
    }
    emit('update:input-mappings', newMappings);
  }
});


// --- 输出映射转换 ---
// 逻辑更复杂，但原理相同
const computedOutputItems = computed({
      // GET: 从 Dictionary<string, string[]> 转换为扁平的 Array<{key, value}>
      get()
      {
        const items: { key: string; value: string }[] = [];
        for (const [key, values] of Object.entries(props.outputMappings))
        {
          if (values.length > 0)
          {
            for (const value of values)
            {
              items.push({
                key,   // 内部变量
                value, // 外部端点
              });
            }
          }
          else
          {
            // 如果一个 key 对应一个空数组，也应该被表示为一个空行，以便用户可以继续编辑
            items.push({key, value: ''});
          }
        }
        return items;
      },
      // SET: 从扁平的 Array<{key, value}> 转换回聚合的 Dictionary<string, string[]>
      set(items)
      {
        const newMappings: OutputMappings = {};
        for (const item of items)
        {
          // 如果此内部变量是第一次出现，则初始化一个空数组
          if (!newMappings[item.key])
          {
            newMappings[item.key] = [];
          }
          // 将外部端点添加到数组中 (注意去重，以防万一)
          if (item.value && !newMappings[item.key].includes(item.value))
          {
            newMappings[item.key].push(item.value);
          }
        }
        emit('update:output-mappings', newMappings);
      }
    })
;
</script>