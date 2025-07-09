<!-- src/app-workbench/components/.../StepAiConfigEditor.vue -->
<template>
  <n-card
      :content-style="{padding:0}"
      :header-style="{ padding: '4px 16px' }"
      size="small"
  >
    <template #header>
      <div style="display: flex; justify-content: space-between; align-items: center;">
        <span>步骤 AI 服务配置</span>
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
            启用并配置步骤 AI 服务
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
                删除步骤 AI 服务
              </n-popover>
            </template>
            确定要禁用并删除此步骤的 AI 服务配置吗？
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

    <!-- 可折叠的内容区域 -->
    <n-collapse-transition :show="isExpanded">
      <!-- 当 config (即 props.modelValue) 存在时，显示配置表单 -->
      <div v-if="config" :style="{ padding: isExpanded && config ? '12px 16px' : '0' }"> <!-- 展开时给内容一点上边距 -->
        <n-spin :show="isLoadingAiConfigSets">
          <!-- AI 配置集选择 -->
          <n-form-item-row help="选择一个全局AI配置集作为此步骤AI服务的基础。" label="选择 AI 配置集">
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

          <!-- AI 模型选择 (仅当配置集被选定时显示) -->
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

          <!-- 流式传输开关 (仅当模型被选定时显示) -->
          <n-form-item-row v-if="config.selectedAiModuleType != null" help="是否要求AI服务以流式方式返回结果。" label="流式传输">
            <n-switch v-model:value="config.isStream"/>
          </n-form-item-row>

          <!-- 移除原先底部的 "禁用步骤AI服务" 按钮，已移至 header -->
        </n-spin>
      </div>
      <!-- 当 config 为 undefined (即未启用AI配置时)，折叠区域内不显示任何内容，由 header 的启用按钮控制 -->
    </n-collapse-transition>
    <!-- 移除原先的 "启用并配置步骤 AI 服务" 按钮，已移至 header -->
  </n-card>
</template>

<script lang="ts" setup>
import {computed, onMounted, ref} from 'vue';
import {NAlert, NButton, NCard, NFormItemRow, NIcon, NSelect, NSpin, NSwitch, NText} from 'naive-ui';
import {AddIcon, DeleteIcon, KeyboardArrowDownIcon, KeyboardArrowUpIcon } from '@/utils/icons';
import type {StepAiConfig} from '@/app-workbench/types/generated/workflow-config-api-client';
import {type AiConfigurationSet, AiConfigurationsService} from '@/app-workbench/types/generated/ai-config-api-client';
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore.ts';
import {useAsyncState} from '@vueuse/core';

const props = defineProps<{
  modelValue: StepAiConfig | undefined;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: StepAiConfig | undefined): void;
}>();

// 控制折叠状态的 ref，默认为展开
const isExpanded = ref(true);

const workbenchStore = useWorkbenchStore();

const config = computed({
  get: () => props.modelValue,
  set: (newValue) =>
  {
    emit('update:modelValue', newValue);
  }
});

const {state: allAiConfigSets, isLoading: isLoadingAiConfigSets, execute: fetchAllAiConfigSets} = useAsyncState(
    () => AiConfigurationsService.getApiAiConfigurations(),
    {} as Record<string, AiConfigurationSet>,
    {immediate: false}
);

onMounted(() =>
{
  fetchAllAiConfigSets();
});

const aiConfigSetOptions = computed(() =>
{
  return Object.entries(allAiConfigSets.value || {}).map(([uuid, set]) => ({
    label: set.configSetName,
    value: uuid,
  }));
});

const currentSelectedAiConfigSet = computed<AiConfigurationSet | null>(() =>
{
  // 同时检查 null 和 undefined
  if (!config.value || config.value.aiProcessorConfigUuid == null) return null;
  const uuid = config.value.aiProcessorConfigUuid;
  return (allAiConfigSets.value && uuid) ? allAiConfigSets.value[uuid] : null;
});

const aiModuleTypeOptions = computed(() =>
{
  if (!currentSelectedAiConfigSet.value || !config.value) return [];
  return Object.keys(currentSelectedAiConfigSet.value.configurations).map(typeKey => ({
    label: workbenchStore.moduleMetadata[typeKey]?.classLabel || typeKey,
    value: typeKey,
  }));
});

const handleConfigSetChange = (newUuid: string | null | undefined) =>
{ // newUuid 也可能为 undefined
  if (config.value)
  {
    // 确保在访问 config.value 之前，它已经被初始化（即用户点击了“启用”）
    // 如果 config.value 存在，那么它的属性 aiProcessorConfigUuid 和 selectedAiModuleType 就可以被安全地赋值为 null
    const selectedSet = newUuid ? allAiConfigSets.value?.[newUuid] : null;
    if (!selectedSet || Object.keys(selectedSet.configurations).length === 0)
    {
      config.value.selectedAiModuleType = null;
    }
    else
    {
      // 同时检查 null 和 undefined
      if (config.value.selectedAiModuleType != null &&
          !selectedSet.configurations.hasOwnProperty(config.value.selectedAiModuleType))
      {
        config.value.selectedAiModuleType = null;
      }
    }
  }
};

const handleEnableAiConfig = () =>
{
  emit('update:modelValue', {
    // 当启用时，显式地将可选属性设为 null 或一个有意义的初始空状态，而不是 undefined。
    // 这样可以确保 config.value 对象始终具有这些属性，即使它们的值是空的。
    // 如果你的业务逻辑允许它们在启用后仍然是 undefined，那么这里可以设为 undefined。
    // 但通常设为 null 更易于后续处理，因为你可以确定属性存在。
    aiProcessorConfigUuid: null,
    selectedAiModuleType: null,
    isStream: false,
  });
};

const handleDisableAiConfig = () =>
{
  emit('update:modelValue', undefined);
};

</script>

<style scoped>
.n-card {
  background-color: #fcfdff;
  border: 1px solid #f0f3f5;
}
</style>