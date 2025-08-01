<template>
  <n-card
      :content-style="{padding:0}"
      :header-style="{ padding: '4px 16px' }"
      size="small"
  >
    <template #header>
      <div style="display: flex; justify-content: space-between; align-items: center;">
        <span>AI 服务配置</span>
        <n-space align="center">
          <!-- 启用/禁用按钮 -->
          <n-popover v-if="!config" trigger="hover">
            <template #trigger>
              <n-button circle size="small" type="primary" @click="handleEnableAiConfig">
                <template #icon>
                  <n-icon :component="AddIcon"/>
                </template>
              </n-button>
            </template>
            启用并配置 AI 服务
          </n-popover>
          <n-popconfirm
              v-if="config"
              negative-text="取消"
              positive-text="确认禁用"
              @positive-click="handleDisableAiConfig"
          >
            <template #trigger>
              <n-popover trigger="hover">
                <template #trigger>
                  <n-button circle size="small" type="error">
                    <template #icon>
                      <n-icon :component="DeleteIcon"/>
                    </template>
                  </n-button>
                </template>
                禁用 AI 服务配置
              </n-popover>
            </template>
            确定要禁用并清空此模块的 AI 服务配置吗？
          </n-popconfirm>

          <!-- 折叠按钮 (仅当配置存在时显示) -->
          <n-popover v-if="config" trigger="hover">
            <template #trigger>
              <n-button circle size="small" @click="isExpanded = !isExpanded">
                <template #icon>
                  <n-icon :component="isExpanded ? KeyboardArrowUpIcon : KeyboardArrowDownIcon"/>
                </template>
              </n-button>
            </template>
            {{ isExpanded ? '收起配置' : '展开配置' }}
          </n-popover>
        </n-space>
      </div>
    </template>

    <n-collapse-transition :show="isExpanded">
      <div v-if="config" :style="{ padding: isExpanded && config ? '12px 16px' : '0' }">
        <n-spin :show="isLoadingAiConfigSets">
          <n-form-item-row help="选择一个全局AI配置集作为此AI服务的基础。" label="选择 AI 配置集">
            <n-select
                v-model:value="config.aiProcessorConfigUuid"
                :options="aiConfigSetOptions"
                clearable
                placeholder="请选择 AI 配置集"
                @update:value="handleConfigSetChange"
            />
          </n-form-item-row>
          <n-alert v-if="!isLoadingAiConfigSets && aiConfigSetOptions.length === 0" :show-icon="true"
                   style="margin-top: 8px; margin-bottom: 8px;" title="无可用AI配置集"
                   type="warning">
            系统中没有找到可用的 AI 配置集。请先在“全局AI配置”中创建。
          </n-alert>

          <n-form-item-row v-if="config.aiProcessorConfigUuid != null" help="从选定的配置集中选择一个具体的AI模型。" label="选择 AI 模型">
            <n-select
                v-model:value="config.selectedAiModuleType"
                :disabled="!config.aiProcessorConfigUuid || aiModuleTypeOptions.length === 0"
                :options="aiModuleTypeOptions"
                clearable
                placeholder="请选择 AI 模型"
            />
            <n-text v-if="aiModuleTypeOptions.length === 0 && currentSelectedAiConfigSet" depth="3"
                    style="font-size: 12px; margin-top: 4px;">
              选中的配置集 “{{ currentSelectedAiConfigSet.configSetName }}” 内没有可用的 AI 模型。
            </n-text>
          </n-form-item-row>

          <n-form-item-row v-if="config.selectedAiModuleType != null" help="是否要求AI服务以流式方式返回结果。" label="流式传输">
            <n-switch v-model:value="config.isStream"/>
          </n-form-item-row>
        </n-spin>
      </div>
    </n-collapse-transition>
  </n-card>
</template>

<script lang="ts" setup>
import {computed, onMounted, ref} from 'vue';
import {NAlert, NButton, NCard, NCollapseTransition, NFormItemRow, NIcon, NPopconfirm, NPopover, NSelect, NSpin, NSwitch, NText} from 'naive-ui';
import {Add as AddIcon, TrashOutline as DeleteIcon, ChevronUp as KeyboardArrowUpIcon, ChevronDown as KeyboardArrowDownIcon} from '@vicons/ionicons5';
// 定义新类型，或者从生成的文件中导入
// 为了解耦，最好在这里本地定义
interface AiModuleSpecificConfig {
  aiProcessorConfigUuid: string | null;
  selectedAiModuleType: string | null;
  isStream: boolean;
}

import {type AiConfigurationSet, AiConfigurationsService} from '@/app-workbench/types/generated/ai-config-api-client';
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore.ts';
import {useAsyncState} from '@vueuse/core';

// 这是一个自定义表单字段，所以它接收 modelValue 并发出 update:modelValue
const props = defineProps<{
  modelValue: AiModuleSpecificConfig | undefined;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: AiModuleSpecificConfig | undefined): void;
}>();

const isExpanded = ref(true);
const workbenchStore = useWorkbenchStore();

// 使用一个可写的计算属性来代理 props.modelValue，这是 v-model 的标准实践
const config = computed({
  get: () => props.modelValue,
  set: (newValue) => {
    emit('update:modelValue', newValue);
  }
});

const {state: allAiConfigSets, isLoading: isLoadingAiConfigSets, execute: fetchAllAiConfigSets} = useAsyncState(
    () => AiConfigurationsService.getApiAiConfigurations(),
    {} as Record<string, AiConfigurationSet>,
    {immediate: false}
);

onMounted(() => {
  fetchAllAiConfigSets();
  // 如果初始就有配置，则保持展开
  isExpanded.value = !!props.modelValue;
});

const aiConfigSetOptions = computed(() => {
  return Object.entries(allAiConfigSets.value || {}).map(([uuid, set]) => ({
    label: set.configSetName,
    value: uuid,
  }));
});

const currentSelectedAiConfigSet = computed<AiConfigurationSet | null>(() => {
  if (!config.value || config.value.aiProcessorConfigUuid == null) return null;
  const uuid = config.value.aiProcessorConfigUuid;
  return (allAiConfigSets.value && uuid) ? allAiConfigSets.value[uuid] : null;
});

const aiModuleTypeOptions = computed(() => {
  if (!currentSelectedAiConfigSet.value || !config.value) return [];
  return Object.keys(currentSelectedAiConfigSet.value.configurations).map(typeKey => ({
    label: workbenchStore.moduleMetadata[typeKey]?.classLabel || typeKey,
    value: typeKey,
  }));
});

const handleConfigSetChange = (newUuid: string | null) => {
  if (config.value) {
    const selectedSet = newUuid ? allAiConfigSets.value?.[newUuid] : null;
    if (!selectedSet || Object.keys(selectedSet.configurations).length === 0) {
      config.value.selectedAiModuleType = null;
    } else {
      if (config.value.selectedAiModuleType != null &&
          !selectedSet.configurations.hasOwnProperty(config.value.selectedAiModuleType)) {
        config.value.selectedAiModuleType = null;
      }
    }
  }
};

const handleEnableAiConfig = () => {
  // 发出事件来创建一个新的、有默认值的配置对象
  emit('update:modelValue', {
    aiProcessorConfigUuid: null,
    selectedAiModuleType: null,
    isStream: false,
  });
  isExpanded.value = true;
};

const handleDisableAiConfig = () => {
  // 发出 undefined 来“删除”这个配置
  emit('update:modelValue', undefined);
};

</script>

<style scoped>
.n-card {
  background-color: #fcfdff;
  border: 1px solid #f0f3f5;
  /* 确保在 DynamicFormRenderer 中不会有奇怪的外边距 */
  margin-top: 4px;
}
</style>