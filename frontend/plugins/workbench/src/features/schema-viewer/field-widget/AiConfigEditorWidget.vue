<!-- AiConfigEditorWidget.vue -->
<template>
  <!-- 整个组件用一个 n-flex 包裹，方便垂直布局 -->
  <n-flex vertical>
    <!-- 上半部分：原有的配置选择区域 -->
    <n-card
        :content-style="{padding:0}"
        :header-style="{ padding: '4px 16px' }"
        size="small"
    >
      <template #header>
        <div style="display: flex; justify-content: space-between; align-items: center;">
          <span>AI 服务配置</span>
          <n-flex align="center">
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
              确定要禁用并清空此符文的 AI 服务配置吗？
            </n-popconfirm>

            <!-- 折叠按钮 -->
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
          </n-flex>
        </div>
      </template>

      <n-collapse-transition :show="isExpanded">
        <div v-if="config" :style="{ padding: isExpanded && config ? '12px 16px' : '0' }">
          <n-spin :show="isLoadingAiConfigSets">
            <!-- 表单内容保持不变 -->
            <n-form-item-row help="选择一个全局AI配置集作为此AI服务的基础。" label="选择 AI 配置集">
              <n-select
                  v-model:value="config.aiProcessorConfigUuid"
                  :options="configSetOptions"
                  clearable
                  placeholder="请选择 AI 配置集"
                  @update:value="handleConfigSetChange"
              />
            </n-form-item-row>
            <n-alert v-if="!isLoadingAiConfigSets && configSetOptions.length === 0" :show-icon="true"
                     style="margin-top: 8px; margin-bottom: 8px;" title="无可用AI配置集"
                     type="warning">
              系统中没有找到可用的 AI 配置集。请先在“全局AI配置”中创建。
            </n-alert>

            <n-form-item-row v-if="config.aiProcessorConfigUuid != null" help="从选定的配置集中选择一个具体的AI模型。" label="选择 AI 模型">
              <n-select
                  v-model:value="config.selectedAiRuneType"
                  :disabled="!config.aiProcessorConfigUuid || aiRuneTypeOptions.length === 0"
                  :options="aiRuneTypeOptions"
                  clearable
                  placeholder="请选择 AI 模型"
              />
              <n-text v-if="aiRuneTypeOptions.length === 0 && currentSelectedAiConfigSet" depth="3"
                      style="font-size: 12px; margin-top: 4px;">
                选中的配置集 “{{ currentSelectedAiConfigSet.configSetName }}” 内没有可用的 AI 模型。
              </n-text>
            </n-form-item-row>

            <n-form-item-row v-if="config.selectedAiRuneType != null" help="是否要求AI服务以流式方式返回结果。" label="流式传输">
              <n-switch v-model:value="config.isStream"/>
            </n-form-item-row>

            <!-- 新增：管理配置集按钮 -->
            <div style="margin-top: 16px; text-align: right;">
              <n-button size="small" tertiary @click="showEditorPanel = !showEditorPanel">
                <template #icon>
                  <n-icon :component="SettingsIcon"/>
                </template>
                {{ showEditorPanel ? '关闭配置管理器' : '管理全局配置集' }}
              </n-button>
            </div>

          </n-spin>
        </div>
      </n-collapse-transition>
    </n-card>

    <!-- 下半部分：可折叠的 AiConfigEditorPanel -->
    <n-collapse-transition :show="showEditorPanel">
      <div class="embedded-panel-container">
        <ai-config-editor-panel/>
      </div>
    </n-collapse-transition>
  </n-flex>
</template>

<script lang="ts" setup>
import {computed, onMounted, ref} from 'vue';
import {
  NAlert,
  NButton,
  NCard,
  NCollapseTransition,
  NFlex,
  NFormItemRow,
  NIcon,
  NPopconfirm,
  NPopover,
  NSelect,
  NSpin,
  NSwitch,
  NText
} from 'naive-ui';
import {AddIcon, DeleteIcon, KeyboardArrowDownIcon, KeyboardArrowUpIcon, SettingsIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {storeToRefs} from 'pinia';

// --- 组件导入 ---
import AiConfigEditorPanel from '#/features/ai-config-panel/AiConfigEditorPanel.vue';
// --- Pinia Stores ---
import {useAiConfigurationStore} from '#/features/ai-config-panel/useAiConfigurationStore';
import {useAiConfigSchemaStore} from '#/features/ai-config-panel/aiConfigSchemaStore';
import type {AiConfigurationSet} from "#/types/generated/ai-config-api-client";
import {useVModel} from "@vueuse/core";

// --- 数据模型定义 ---
interface AiRuneSpecificConfig
{
  aiProcessorConfigUuid: string | null;
  selectedAiRuneType: string | null;
  isStream: boolean;
}

// --- Props and Emits (v-model) ---
const props = defineProps<{
  modelValue: AiRuneSpecificConfig | undefined;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: AiRuneSpecificConfig | undefined): void;
}>();

// --- 本地 UI 状态 ---
const isExpanded = ref(true);
const showEditorPanel = ref(false); // 新增状态，控制配置面板的显示

// --- Store 实例化 ---
const configStore = useAiConfigurationStore();
const schemaStore = useAiConfigSchemaStore();
const {
  allConfigSets,
  isLoading: isLoadingAiConfigSets,
  configSetOptions
} = storeToRefs(configStore);

// --- v-model 代理 ---
const config = useVModel(props, 'modelValue', emit);

onMounted(() =>
{
  configStore.fetchAllConfigSets();
  schemaStore.fetchAllDefinitions();
  isExpanded.value = !!props.modelValue;
});

// --- Computed Properties ---
const currentSelectedAiConfigSet = computed<AiConfigurationSet | null>(() =>
{
  if (!config.value?.aiProcessorConfigUuid) return null;
  return allConfigSets.value[config.value.aiProcessorConfigUuid] ?? null;
});

const aiRuneTypeOptions = computed(() =>
{
  if (!currentSelectedAiConfigSet.value) return [];
  const configuredTypes = Object.keys(currentSelectedAiConfigSet.value.configurations);
  return schemaStore.availableTypesOptions
      .filter(option => configuredTypes.includes(option.value as string))
      .map(option => ({label: option.label, value: option.value,}));
});


// --- 方法 (保持不变) ---
const handleConfigSetChange = (newUuid: string | null) =>
{
  if (config.value)
  {
    const selectedSet = newUuid ? allConfigSets.value?.[newUuid] : null;
    if (!selectedSet || Object.keys(selectedSet.configurations).length === 0)
    {
      config.value.selectedAiRuneType = null;
    }
    else
    {
      if (config.value.selectedAiRuneType && !selectedSet.configurations.hasOwnProperty(config.value.selectedAiRuneType))
      {
        config.value.selectedAiRuneType = null;
      }
    }
  }
};

const handleEnableAiConfig = () =>
{
  emit('update:modelValue', {
    aiProcessorConfigUuid: null,
    selectedAiRuneType: null,
    isStream: false,
  });
  isExpanded.value = true;
};

const handleDisableAiConfig = () =>
{
  emit('update:modelValue', undefined);
};

</script>

<style scoped>
/* 为嵌入的面板添加一个容器样式，使其看起来更融合 */
.embedded-panel-container {
  margin-top: 16px;
  padding: 16px;
  border: 1px solid #e0e0e6;
  border-radius: 8px;
}
</style>