<!-- src/app-workbench/components/editor/StepAiConfigEditor.vue -->
<template>
  <n-card size="small" title="步骤 AI 服务配置">
    <!-- 当 config (即 props.modelValue) 存在时，显示配置表单 -->
    <div v-if="config">
      <n-spin :show="isLoadingAiConfigSets">
        <!-- AI 配置集选择 -->
        <n-form-item-row label="选择 AI 配置集" help="选择一个全局AI配置集作为此步骤AI服务的基础。">
          <n-select
              v-model:value="config.aiProcessorConfigUuid"
              placeholder="请选择 AI 配置集"
              :options="aiConfigSetOptions"
              clearable
              @update:value="handleConfigSetChange"
          />
          <!-- 当配置集改变时，触发处理函数 -->
        </n-form-item-row>
        <!-- 当没有AI配置集时的提示 -->
        <n-alert v-if="!isLoadingAiConfigSets && aiConfigSetOptions.length === 0" title="无可用AI配置集" type="warning" :show-icon="true" style="margin-top: 8px; margin-bottom: 8px;">
          系统中没有找到可用的 AI 配置集。请先在“全局AI配置”中创建。
        </n-alert>

        <!-- AI 模型选择 (仅当配置集被选定时显示) -->
        <n-form-item-row v-if="config.aiProcessorConfigUuid" label="选择 AI 模型" help="从选定的配置集中选择一个具体的AI模型。">
          <n-select
              v-model:value="config.selectedAiModuleType"
              placeholder="请选择 AI 模型"
              :options="aiModuleTypeOptions"
              :disabled="!config.aiProcessorConfigUuid || aiModuleTypeOptions.length === 0"
              clearable
          />
          <!-- 当选中的配置集没有可用模型时的提示 -->
          <n-text v-if="config.aiProcessorConfigUuid && aiModuleTypeOptions.length === 0 && currentSelectedAiConfigSet" depth="3" style="font-size: 12px; margin-top: 4px;">
            选中的配置集 “{{ currentSelectedAiConfigSet.configSetName }}” 内没有可用的 AI 模型。
          </n-text>
        </n-form-item-row>

        <!-- 流式传输开关 (仅当模型被选定时显示) -->
        <n-form-item-row v-if="config.selectedAiModuleType" label="流式传输" help="是否要求AI服务以流式方式返回结果。">
          <n-switch v-model:value="config.isStream" />
        </n-form-item-row>

        <!-- 禁用按钮 -->
        <n-button type="error" text block @click="handleDisableAiConfig" style="margin-top: 16px;">
          <template #icon><n-icon :component="DeleteIcon"/></template>
          禁用步骤AI服务
        </n-button>
      </n-spin>
    </div>
    <!-- 当 config (即 props.modelValue) 为 undefined 时，显示启用按钮 -->
    <n-button v-else block dashed @click="handleEnableAiConfig" type="primary">
      <template #icon><n-icon :component="AddIcon"/></template>
      启用并配置步骤 AI 服务
    </n-button>
  </n-card>
</template>

<script lang="ts" setup>
import { computed, onMounted, watch } from 'vue'; // 移除了 ref，因为 config 现在是 computed
import { NCard, NSpin, NFormItemRow, NSelect, NSwitch, NButton, NIcon, NAlert, NText } from 'naive-ui';
import { DeleteOutlineRound as DeleteIcon, AddCircleOutlineRound as AddIcon } from '@vicons/material';
import type { StepAiConfig } from '@/app-workbench/types/generated/workflow-config-api-client';
import { AiConfigurationsService, type AiConfigurationSet } from '@/app-workbench/types/generated/ai-config-api-client';
import { useWorkbenchStore } from '@/app-workbench/stores/workbenchStore.ts';
import { useAsyncState } from '@vueuse/core';

// --- Props 和 Emits ---
const props = defineProps<{
  modelValue: StepAiConfig | undefined; // 接收父组件传递的 stepAiConfig 对象
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: StepAiConfig | undefined): void; // 用于更新父组件的 stepAiConfig
}>();

// --- 内部状态和逻辑 ---
const workbenchStore = useWorkbenchStore();

// 使用 computed 实现 props.modelValue 的双向绑定效果
// 当父组件的 modelValue 改变时，config 会自动更新
// 当在组件内部修改 config.value 时，set 函数会被调用，从而 emit 事件通知父组件
const config = computed({
  get: () => props.modelValue,
  set: (newValue) => {
    emit('update:modelValue', newValue);
  }
});

// 异步获取所有 AI 配置集
const { state: allAiConfigSets, isLoading: isLoadingAiConfigSets, execute: fetchAllAiConfigSets } = useAsyncState(
    () => AiConfigurationsService.getApiAiConfigurations(),
    {} as Record<string, AiConfigurationSet>,
    { immediate: false } // 不立即执行，在 onMounted 中调用
);

onMounted(() => {
  fetchAllAiConfigSets(); // 组件挂载时获取 AI 配置集数据
});

// 计算属性：将 AI 配置集转换为 NSelect 需要的选项格式
const aiConfigSetOptions = computed(() => {
  return Object.entries(allAiConfigSets.value || {}).map(([uuid, set]) => ({
    label: set.configSetName,
    value: uuid,
  }));
});

// 计算属性：获取当前选中的 AI 配置集对象
const currentSelectedAiConfigSet = computed<AiConfigurationSet | null>(() => {
  if (!config.value || !config.value.aiProcessorConfigUuid) return null;
  const uuid = config.value.aiProcessorConfigUuid;
  return (allAiConfigSets.value && uuid) ? allAiConfigSets.value[uuid] : null;
});

// 计算属性：根据当前选中的配置集，获取可用的 AI 模型类型选项
const aiModuleTypeOptions = computed(() => {
  if (!currentSelectedAiConfigSet.value || !config.value) return [];
  // 从配置集的 configurations 中提取 moduleType 作为 value，
  // 并尝试从 workbenchStore.moduleMetadata 获取友好的 classLabel 作为 label
  return Object.keys(currentSelectedAiConfigSet.value.configurations).map(typeKey => ({
    label: workbenchStore.moduleMetadata[typeKey]?.classLabel || typeKey,
    value: typeKey,
  }));
});

/**
 * 当用户选择的 AI 配置集发生变化时调用。
 * 主要用于清空或验证已选择的 AI 模型。
 * @param newUuid - 新选择的 AI 配置集的 UUID，如果清空选择则为 null。
 */
const handleConfigSetChange = (newUuid: string | null) => {
  if (config.value) { // 确保当前的 config 对象存在
    const selectedSet = newUuid ? allAiConfigSets.value?.[newUuid] : null;
    // 如果没有选择配置集，或者选择的配置集无效/没有具体的AI模型配置
    if (!selectedSet || Object.keys(selectedSet.configurations).length === 0) {
      config.value.selectedAiModuleType = null; // 清空已选的 AI 模型
    } else {
      // 如果之前选中的 AI 模型在新选择的配置集中不存在，则清空
      if (config.value.selectedAiModuleType &&
          !selectedSet.configurations.hasOwnProperty(config.value.selectedAiModuleType)) {
        config.value.selectedAiModuleType = null;
      }
    }
  }
};

// 点击“启用并配置步骤 AI 服务”按钮时调用
const handleEnableAiConfig = () => {
  // 发送一个包含初始默认值的 StepAiConfig 对象给父组件
  emit('update:modelValue', {
    aiProcessorConfigUuid: null,    // 初始未选择配置集
    selectedAiModuleType: null, // 初始未选择模型
    isStream: false,                // 默认非流式
  });
};

// 点击“禁用步骤AI服务”按钮时调用
const handleDisableAiConfig = () => {
  // 发送 undefined 给父组件，表示移除/禁用 AI 配置
  emit('update:modelValue', undefined);
};

// 注意：由于 config 是一个 computed 属性，其 get 和 set 直接与 props.modelValue 和 emit 交互，
// 所以不再需要像之前那样显式地 watch props.modelValue 来同步内部 ref。
// Vue 的响应式系统会处理好这一切。

</script>

<style scoped>
/* 为卡片添加一个稍微不同的背景色，以在视觉上与步骤的其他部分区分开 */
.n-card {
  background-color: #fcfdff; /* 一个非常浅的蓝色或灰色调 */
  border: 1px solid #f0f3f5;
}
/* 可以根据需要添加更多组件特定的样式 */
</style>