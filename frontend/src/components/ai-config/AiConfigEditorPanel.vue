<template>
  <n-spin :show="componentLoading > 0">
    <n-flex vertical size="large">
      <!-- 上方控制区域：配置集选择与操作 -->
      <n-card title="AI 配置集管理">
        <n-flex align="center" justify="space-between" style="margin-bottom: 16px;">
          <n-form-item label="选择配置集" style="flex-grow: 1;margin-bottom: 0;">
            <n-select
                :value="selectedConfigSetUuid"
                :options="configSetOptions"
                placeholder="请选择一个配置集"
                clearable
                @update:value="handleConfigSetSelectionChange"
            />
          </n-form-item>
          <n-flex>
            <n-button type="primary" @click="handleSaveConfigSet" :disabled="!canSaveChanges">
              保存变更
            </n-button>
            <n-button @click="promptCreateNewSet">新建配置集</n-button>
            <n-button @click="promptCloneSet" :disabled="!selectedConfigSetUuid">复制当前</n-button>
            <n-button @click="promptRenameSet" :disabled="!selectedConfigSetUuid">修改名称</n-button>
            <n-popconfirm @positive-click="executeDeleteSet" :disabled="!selectedConfigSetUuid">
              <template #trigger>
                <n-button type="error" :disabled="!selectedConfigSetUuid">删除当前</n-button>
              </template>
              确定要删除选中的配置集 "{{ currentConfigSet?.configSetName }}" 吗？此操作不可恢复。
            </n-popconfirm>
          </n-flex>
        </n-flex>
      </n-card>

      <!-- 中间区域：AI 类型选择 -->
      <n-card v-if="currentConfigSet" title="AI 模型配置">
        <!-- 使用 NFlex 将下拉框和按钮放在同一行 -->
        <n-flex justify="space-between" align="center">
          <n-form-item label="选择或添加 AI 模型类型" style="flex-grow: 1;">
            <n-select
                :value="selectedAiModuleType"
                :options="aiTypeOptionsWithMarker"
                placeholder="选择 AI 模型类型"
                clearable
                :disabled="availableAiTypes.length === 0"
                @update:value="handleAiTypeSelectionChange"
            />
            <div v-if="availableAiTypes.length === 0 && componentLoading === 0" style="color: grey; font-size: 12px; margin-top: 4px;">
              未能加载到可用的AI模型类型。
            </div>
          </n-form-item>

          <n-popconfirm @positive-click="handleRemoveCurrentAiConfig">
            <template #trigger>
              <n-button type="warning" tertiary
                        :disabled="!selectedAiModuleType || !currentConfigSet?.configurations?.[selectedAiModuleType]">
                移除当前模型配置 <!-- 按钮文本修改 -->
              </n-button>
            </template>
            确定要从配置集 "{{ currentConfigSet?.configSetName }}" 中移除模型 "{{ selectedAiModuleType }}" 的配置吗？
            此操作将删除该模型的定制配置，恢复到使用全局默认值，需要点击“保存变更”才会生效。 <!-- 提示文本修改 -->
          </n-popconfirm>

          <!-- AI 配置测试组件 -->
          <!-- 仅当选择了配置集和 AI 模型类型时显示测试器 -->
          <ai-config-tester
              v-if="selectedConfigSetUuid && selectedAiModuleType && currentConfigSet"
              :form-data-copy="formDataCopy"
              :config-set-name="currentConfigSet?.configSetName || '未知配置集'"
              :module-type="selectedAiModuleType"
              :key="`${selectedConfigSetUuid}-${selectedAiModuleType}`"
          />
        </n-flex>
      </n-card>

      <!-- 下方区域：动态表单 -->
      <n-card v-if="currentConfigSet && selectedAiModuleType && rawSchemaForForm"
              :title="rawSchemaForForm.description"> <!-- 假设 description 仍然在原始 schema 中 -->
        <dynamic-form-renderer
            v-if="typeof formDataCopy === 'object'"
            :key="formRenderKey"
            :schema="rawSchemaForForm"
            v-model="formDataCopy"
            :form-props="formGlobalProps"
            :is-loading="isCurrentSchemaLoading"
            :loading-description="'正在加载模型 Schema...'"
            :error-message="currentSchemaFetchError??undefined"
            @change="checkFormChange"
            ref="dynamicFormRendererRef"
        />
        <n-empty
            v-else-if="!isCurrentSchemaLoading && typeof formDataCopy !== 'object'"
            description="请先添加或配置此模型类型。"
        />
      </n-card>

      <!-- 整体空状态 -->
      <n-empty v-if="!selectedConfigSetUuid && Object.keys(allConfigSets).length > 0 && componentLoading === 0"
               description="请先选择一个AI配置集。"/>
      <n-empty v-else-if="Object.keys(allConfigSets).length === 0 && componentLoading === 0" description="暂无配置集，请新建一个。"/>
      <n-empty v-else-if="currentConfigSet && !selectedAiModuleType && componentLoading === 0"
               description="请选择一个AI模型类型进行配置。"/>

    </n-flex>
  </n-spin>
</template>

<script setup lang="ts">
import {computed, defineAsyncComponent, h, markRaw, nextTick, onMounted, reactive, ref, watch} from 'vue';
import {
  NButton,
  NCard,
  NEmpty,
  NFormItem,
  NInput,
  NPopconfirm,
  NSelect,
  NSpace,
  NSpin,
  type SelectOption as NaiveSelectOption,
  useDialog,
  useMessage
} from 'naive-ui';
import VueForm from '@lljj/vue3-form-naive'; // 确认此库的准确导入名称和方式
import {cloneDeep, isEqual} from 'lodash-es';
import {AiConfigurationsService} from '@/types/generated/aiconfigapi/services/AiConfigurationsService';
import {AiConfigSchemasService} from '@/types/generated/aiconfigapi/services/AiConfigSchemasService';
import type {AiConfigurationSet} from '@/types/generated/aiconfigapi/models/AiConfigurationSet';
import type {SelectOptionDto as ApiSelectOption} from '@/types/generated/aiconfigapi/models/SelectOptionDto.ts';
import type {AbstractAiProcessorConfig} from "@/types/generated/aiconfigapi/models/AbstractAiProcessorConfig";
import {useAiConfigSchemaStore} from "@/components/ai-config/schemaStore.ts";
import AiConfigTester from "@/components/ai-config/AiConfigTester.vue";
import DynamicFormRenderer, {type DynamicFormRendererInstance} from "@/components/schema/DynamicFormRenderer.vue";
import {useAiConfigActions} from "@/components/ai-config/useAiConfigActions.ts";


// ----------- 全局状态与工具 -----------
const message = useMessage();
const dialog = useDialog();
const componentLoading = ref(0); // 活动API调用计数器
const schemaStore = useAiConfigSchemaStore();
const dynamicFormRendererRef = ref<DynamicFormRendererInstance | null>(null);

// ----------- 配置集状态 -----------
// 使用 reactive 包裹 Record 本身，使其内部属性的增删也能被侦测
const allConfigSets = reactive<Record<string, AiConfigurationSet>>({});
const formDataCopy = ref<AbstractAiProcessorConfig | null>(null); // **新增**: 用于存储当前编辑表单的数据副本
const selectedConfigSetUuid = ref<string | null>(null);
const formChanged = ref(false); // 标记当前表单是否有未保存的修改
const rawSchemaForForm = ref<Record<string, any> | null>(null); // 新增：用于存储传递给 DynamicFormRenderer 的原始 Schema

const currentConfigSet = computed<AiConfigurationSet | null>(() => {
  if (selectedConfigSetUuid.value && allConfigSets[selectedConfigSetUuid.value]) {
    return allConfigSets[selectedConfigSetUuid.value];
  }
  return null;
});

const configSetOptions = computed<NaiveSelectOption[]>(() =>
    Object.entries(allConfigSets).map(([uuid, set]) => ({
      label: set.configSetName,
      value: uuid
    }))
);

const isCurrentSchemaLoading = computed(() => {
  return selectedAiModuleType.value ? schemaStore.isSchemaLoading(selectedAiModuleType.value) : false;
});

const currentSchemaFetchError = computed(() => {
  return selectedAiModuleType.value ? schemaStore.getSchemaError(selectedAiModuleType.value) : null;
});

const canSaveChanges = computed(() => {
  // 必须选中一个配置集，并且表单有变动
  return !!selectedConfigSetUuid.value && formChanged.value;
});

// ----------- AI 类型与 Schema 状态 -----------
const availableAiTypes = ref<ApiSelectOption[]>([]);
const selectedAiModuleType = ref<string | null>(null);
const currentSchema = ref<Record<string, any> | null>(null);
const formRenderKey = ref(0); // 用于强制重新渲染 vue-form

const formGlobalProps = computed(() => ({
  // 这些是传递给 Naive UI NForm 组件的 props
  labelPlacement: 'top' as const,
  labelWidth: 'auto' as const,
  isMiniDes: true,
}));

// ----------- API 调用封装 -----------
async function callApi<T>(fn: () => Promise<T>, successMessage?: string, autoHandleError = true): Promise<T | undefined> {
  componentLoading.value++;
  try {
    const result = await fn();
    if (successMessage) {
      message.success(successMessage);
    }
    return result;
  } catch (error: any) {
    if (autoHandleError) {
      message.error(`操作失败: ${error.body?.detail || error.message || '未知错误'}`);
      console.error("API Error:", error);
    }
    return undefined;
  } finally {
    componentLoading.value--;
  }
}

// ----------- 数据加载 -----------
async function fetchAllConfigSets() {
  const response = await callApi(() => AiConfigurationsService.getApiAiConfigurations());
  if (response) {
    // 清空旧的, 填充新的，确保响应性
    Object.keys(allConfigSets).forEach(key => delete allConfigSets[key]);
    for (const uuid in response) {
      allConfigSets[uuid] = reactive(response[uuid]); // 确保每个配置集也是响应式的
    }
    // 如果之前有选中，检查是否仍然存在，否则清空选择
    if (selectedConfigSetUuid.value && !allConfigSets[selectedConfigSetUuid.value]) {
      selectedConfigSetUuid.value = null;
    } else if (!selectedConfigSetUuid.value && Object.keys(allConfigSets).length > 0) {
      // 后端保证至少有一个，所以如果当前未选中，可以考虑默认选中第一个
      // selectedConfigSetUuid.value = Object.keys(allConfigSets)[0];
      // 或者保持 null，让用户选择
    }
  } else {
    Object.keys(allConfigSets).forEach(key => delete allConfigSets[key]);
  }
}

async function fetchAvailableAiTypes() {
  const response = await callApi(() => AiConfigSchemasService.getApiAiConfigurationManagementAvailableConfigTypes());
  if (response) {
    availableAiTypes.value = response;
  }
}

onMounted(async () => {
  await fetchAllConfigSets();
  await fetchAvailableAiTypes();
});

// ----------- 按钮 -----------
const {
  handleSaveConfigSet,
  promptCreateNewSet,
  promptCloneSet,
  promptRenameSet,
  executeDeleteSet,
  handleRemoveCurrentAiConfig,
} = useAiConfigActions({
  allConfigSets,
  selectedConfigSetUuid,
  currentConfigSet,
  selectedAiModuleType,
  currentSchema, // 确保这个 ref 被正确传递和使用
  formDataCopy,
  formChanged,
  rawSchemaForForm,
  dynamicFormRendererRef,
  callApi, // 传递已定义的 callApi 函数
  fetchAllConfigSets, // 传递已定义的 fetchAllConfigSets 函数
  resetFormChangeFlag, // 传递已定义的 resetFormChangeFlag 函数
  message, // 传递 message 实例
  dialog,  // 传递 dialog 实例
});

// ----------- 配置集操作逻辑 -----------
// 封装放弃更改的确认逻辑
async function confirmAbandonChanges(title: string, content: string): Promise<boolean> {
  if (!formChanged.value) return true; // 没有更改，直接允许
  return new Promise<boolean>(resolve => {
    dialog.warning({
      title,
      content,
      positiveText: '确定放弃',
      negativeText: '取消操作',
      onPositiveClick() {
        resetFormChangeFlag();
        return resolve(true);
      },
      onNegativeClick() {
        // resetFormChangeFlag();
        return resolve(false);
      },
      onClose() {
        // resetFormChangeFlag();
        return resolve(false);
      },
    });
  });
}

const aiTypeOptionsWithMarker = computed<NaiveSelectOption[]>(() => {
  return availableAiTypes.value.map(type => {
    const isConfigured = !!(currentConfigSet.value?.configurations && currentConfigSet.value.configurations[type.value as string]);
    return {
      label: `${type.label}${isConfigured ? ' ✔️' : ''}`,
      value: type.value as string, // 确保 value 是 string
    };
  });
});

async function handleConfigSetSelectionChange(newUuid: string | null) {
  const oldUuid = selectedConfigSetUuid.value;
  console.log(`[AttemptChange] User wants to change from ${oldUuid} to ${newUuid}`);
  if (newUuid === oldUuid) return;

  if (oldUuid) { // 如果之前有选中的项
    const canProceed = await confirmAbandonChanges(
        '放弃未保存的更改？',
        `配置集 "${allConfigSets[oldUuid]?.configSetName}" 有未保存的更改。切换配置集将丢失这些更改。`
    );
    if (!canProceed)
      return;
  }

  console.log(`[AttemptChange] User change from ${oldUuid} to ${newUuid}`)
  selectedConfigSetUuid.value = newUuid;
}


// 当配置集选择变化时
watch(selectedConfigSetUuid, async (newUuid, oldUuid) => {
  console.log(`watchConfigSetSelectionChange: ${oldUuid} -> ${newUuid}`)
  if (newUuid === oldUuid) return;

  // selectedConfigSetUuid.value = newUuid; // 更新选择
  selectedAiModuleType.value = null; // 清空AI类型选择
  currentSchema.value = null;      // 清空当前Schema
  formRenderKey.value = 0;         // 重置表单渲染key
})

// 事件处理器，用于拦截 aiTypeSelect 的变化
async function handleAiTypeSelectionChange(newModuleType: string | null) {
  const oldModuleType = selectedAiModuleType.value;
  console.log(`[AttemptChange] User wants to change from ${oldModuleType} to ${newModuleType}`);
  if (newModuleType === oldModuleType) return;

  if (oldModuleType && formDataCopy.value) {
    const canProceed = await confirmAbandonChanges(
        '放弃未保存的更改？',
        `AI模型 "${oldModuleType}" 的配置有未保存的更改。切换类型将丢失这些更改。`
    );
    if (!canProceed)
      return;
  }

  // 如果没有更改，或者用户确认放弃更改，则实际更新 selectedAiModuleType
  // 这会触发下面的 watch
  console.log(`[AttemptChange] User change from ${oldModuleType} to ${newModuleType}`);
  selectedAiModuleType.value = newModuleType;
}


// 当AI模型类型选择变化时
watch(selectedAiModuleType, async (newModuleType, oldModuleType) => {
  console.log(`watchAiTypeSelectionChange: ${oldModuleType} -> ${newModuleType}`)
  if (newModuleType === oldModuleType) return; // 避免重复操作

  if (!newModuleType || !currentConfigSet.value) {
    currentSchema.value = null;
    formDataCopy.value = null;
    return
  }
  console.time(`[Schema Perf] Get/Fetch Schema: ${newModuleType}`);
  const rawSchema = await schemaStore.getOrFetchSchema(newModuleType);
  if (!rawSchema || schemaStore.getSchemaError(newModuleType)) {
    currentSchema.value = null;
    formDataCopy.value = null;
    message.error(`加载模型 "${newModuleType}" 的 Schema 失败。`);
    return
  }
  rawSchemaForForm.value = rawSchema;
  console.timeEnd(`[Schema Perf] Get/Fetch Schema: ${newModuleType}`);
  // -------------------------
  console.time(`[Schema Perf] State Update & Tick: ${newModuleType}`);
  if (!currentConfigSet.value.configurations) {
    currentConfigSet.value.configurations = reactive({});
  }
  let originalConfig = currentConfigSet.value.configurations[newModuleType];
  if (!originalConfig) {
    const initialData: AbstractAiProcessorConfig = 
        (await callApi(() => AiConfigurationsService.getApiAiConfigurationsDefaultData({moduleType: newModuleType}))) ?? {};
    // 让 vue-form 根据（可能已预处理过的）schema 的 default 自动填充
    originalConfig = reactive(initialData);
  }
  formDataCopy.value = cloneDeep(originalConfig);
  formRenderKey.value++;
  await nextTick();
  console.timeEnd(`[Schema Perf] State Update & Tick: ${newModuleType}`);
})

function checkFormChange() {
  if (!selectedAiModuleType.value || !currentConfigSet.value?.configurations || formDataCopy.value === null) {
    formChanged.value = false;
    return;
  }
  const originalData = currentConfigSet.value.configurations[selectedAiModuleType.value];
  // console.log('Form changed:', formDataCopy.value, originalData);
  formChanged.value = !isEqual(originalData, formDataCopy.value);
}

function resetFormChangeFlag() {
  formChanged.value = false;
}

</script>

<style scoped>
.n-card {
  margin-bottom: 20px; /* 增加卡片间距 */
}

.n-form-item {
  margin-bottom: 0; /* 移除表单项的默认底部边距，如果NCard已经提供了足够的间距 */
}

/* 可以添加更多自定义样式 */
</style>