<template>
  <n-spin :show="configStore.isLoading || schemaStore.isLoading">
    <n-flex vertical size="large">
      <!-- 上方控制区域 -->
      <n-card title="AI 配置集管理">
        <n-flex align="center" justify="space-between" style="margin-bottom: 16px;">
          <n-form-item label="选择配置集" style="flex-grow: 1;margin-bottom: 0;">
            <n-select
                v-model:value="selectedUuid"
                :options="configSetOptions"
                placeholder="请选择一个配置集"
                clearable
                @update:value="handleConfigSetSelectionChange"
            />
          </n-form-item>
          <n-flex>
            <n-button type="primary" @click="handleSave" :disabled="!canSaveChanges">保存变更</n-button>
            <n-button @click="promptCreateNewSet">新建配置集</n-button>
            <n-button @click="promptCloneSet" :disabled="!selectedUuid">复制当前</n-button>
            <n-button @click="promptRenameSet" :disabled="!selectedUuid">修改名称</n-button>
            <n-popconfirm @positive-click="executeDeleteSet" :disabled="!selectedUuid">
              <template #trigger>
                <n-button type="error" :disabled="!selectedUuid">删除当前</n-button>
              </template>
              确定要删除选中的配置集 "{{ currentConfigSet?.configSetName }}" 吗？
            </n-popconfirm>
          </n-flex>
        </n-flex>
      </n-card>

      <!-- 中间区域 -->
      <n-card v-if="currentConfigSet" title="AI 模型配置">
        <n-flex justify="space-between" align="center">
          <n-form-item label="选择或添加 AI 模型类型" style="flex-grow: 1;">
            <n-select
                v-model:value="selectedAiModuleType"
                :options="aiTypeOptionsWithMarker"
                placeholder="选择 AI 模型类型"
                clearable
                :disabled="schemaStore.availableTypesOptions.length === 0"
                @update:value="handleAiTypeSelectionChange"
            />
          </n-form-item>
          <n-popconfirm @positive-click="handleRemoveCurrentAiConfig">
            <template #trigger>
              <n-button type="warning" tertiary :disabled="!canRemoveCurrentAiConfig">移除当前模型配置</n-button>
            </template>
            确定要移除模型 "{{ selectedAiModuleType }}" 的配置吗？此操作需点击“保存变更”才会生效。
          </n-popconfirm>
          <ai-config-tester
              v-if="selectedUuid && selectedAiModuleType && currentConfigSet"
              :form-data-copy="formDataCopy"
              :config-set-name="currentConfigSet?.configSetName || '未知'"
              :module-type="selectedAiModuleType"
              :key="`${selectedUuid}-${selectedAiModuleType}`"
          />
        </n-flex>
      </n-card>

      <!-- 下方区域 -->
      <n-card v-if="currentConfigSet && selectedAiModuleType && currentSchema" :title="currentSchema.description">
        <dynamic-form-renderer
            v-if="typeof formDataCopy === 'object'"
            :key="formRenderKey"
            :schema="currentSchema"
            v-model="formDataCopy"
            :form-props="{ labelPlacement: 'top' }"
            @change="checkFormChange"
            ref="dynamicFormRendererRef"
        />
      </n-card>

      <!-- 空状态 -->
      <n-empty v-if="!selectedUuid && !configStore.isLoading" description="请选择或新建一个AI配置集。"/>
      <n-empty v-else-if="currentConfigSet && !selectedAiModuleType && !configStore.isLoading" description="请选择一个AI模型类型进行配置。"/>
    </n-flex>
  </n-spin>
</template>

<script setup lang="ts">
import {computed, h, nextTick, onMounted, ref, watch} from 'vue';
import { NButton, NCard, NEmpty, NFlex, NFormItem, NInput, NPopconfirm, NSelect, NSpin, useDialog, useMessage } from 'naive-ui';
import { storeToRefs } from 'pinia';
import { cloneDeep, isEqual } from 'lodash-es';
import type { AbstractAiProcessorConfig, AiConfigurationSet } from '@/app-workbench/types/generated/ai-config-api-client';
import { useAiConfigurationStore } from "@/app-workbench/features/ai-config-panel/useAiConfigurationStore";
import { useAiConfigSchemaStore } from "@/app-workbench/features/ai-config-panel/aiConfigSchemaStore";
import AiConfigTester from "@/app-workbench/features/ai-config-panel/AiConfigTester.vue";
import DynamicFormRenderer, { type DynamicFormRendererInstance } from "@/app-workbench/features/schema-viewer/DynamicFormRenderer.vue";

// --- Stores and Utils ---
const message = useMessage();
const dialog = useDialog();
const configStore = useAiConfigurationStore();
const schemaStore = useAiConfigSchemaStore();
const { selectedUuid, currentConfigSet, configSetOptions } = storeToRefs(configStore);
const dynamicFormRendererRef = ref<DynamicFormRendererInstance | null>(null);

// --- Component-local State for Editing ---
const selectedAiModuleType = ref<string | null>(null);
const formDataCopy = ref<AbstractAiProcessorConfig | null>(null);
const originalDataForCompare = ref<AbstractAiProcessorConfig | null>(null);
const currentSchema = ref<Record<string, any> | null>(null);
const formRenderKey = ref(0);
const formChanged = ref(false);

// --- Computed Properties ---
const canSaveChanges = computed(() => !!selectedUuid.value && formChanged.value);
const canRemoveCurrentAiConfig = computed(() =>
    !!selectedAiModuleType.value && !!currentConfigSet.value?.configurations?.[selectedAiModuleType.value]
);
const aiTypeOptionsWithMarker = computed(() => {
  return schemaStore.availableTypesOptions.map(type => ({
    ...type,
    label: `${type.label}${currentConfigSet.value?.configurations?.[type.value as string] ? ' ✔️' : ''}`,
  }));
});

// --- Data Loading ---
onMounted(async () => {
  await schemaStore.fetchAllDefinitions();
  await configStore.fetchAllConfigSets();
});

// --- UI Actions (calling store actions) ---
async function handleSave() {
  if (!currentConfigSet.value || !selectedUuid.value) return;

  try {
    await dynamicFormRendererRef.value?.validate();
  } catch (errors) {
    message.error('表单校验失败，请检查字段。');
    return;
  }

  // 构造最新的配置集数据
  const updatedSet = cloneDeep(currentConfigSet.value);
  if (selectedAiModuleType.value && formDataCopy.value) {
    updatedSet.configurations[selectedAiModuleType.value] = formDataCopy.value;
  }

  await configStore.saveConfigSet(updatedSet);

  // 保存成功后，更新用于比较的原始数据
  originalDataForCompare.value = cloneDeep(formDataCopy.value);
  formChanged.value = false;
}

function promptCreateNewSet() {
  const newSetName = ref('');
  dialog.create({
    title: '新建 AI 配置集',
    content: () => h(NInput, { value: newSetName.value, onUpdateValue: val => newSetName.value = val, placeholder: '请输入配置集名称', autofocus: true }),
    positiveText: '创建',
    onPositiveClick: () => {
      if (!newSetName.value.trim()) {
        message.error('名称不能为空');
        return false;
      }
      configStore.createNewSet(newSetName.value.trim());
    }
  });
}

function promptCloneSet() {
  if (!currentConfigSet.value) return;
  const clonedName = ref(`${currentConfigSet.value.configSetName} (副本)`);
  dialog.create({
    title: '复制配置集',
    content: () => h(NInput, { value: clonedName.value, onUpdateValue: val => clonedName.value = val }),
    positiveText: '复制',
    onPositiveClick: () => configStore.cloneCurrentSet(clonedName.value.trim()),
  });
}

function promptRenameSet() {
  if (!currentConfigSet.value) return;
  const newName = ref(currentConfigSet.value.configSetName);
  dialog.create({
    title: '修改配置集名称',
    content: () => h(NInput, { value: newName.value, onUpdateValue: val => newName.value = val }),
    positiveText: '保存',
    onPositiveClick: () => configStore.renameCurrentSet(newName.value.trim()),
  });
}

function executeDeleteSet() {
  configStore.deleteCurrentSet();
}

// --- Local Change Management ---

async function confirmAbandonChanges(): Promise<boolean> {
  if (!formChanged.value) return true;
  return new Promise(resolve => {
    dialog.warning({
      title: '放弃未保存的更改？',
      content: '当前表单有未保存的更改，切换将丢失这些内容。',
      positiveText: '确定放弃',
      negativeText: '取消',
      onPositiveClick: () => resolve(true),
      onClose: () => resolve(false),
      onNegativeClick: () => resolve(false)
    });
  });
}

async function handleConfigSetSelectionChange(newUuid: string | null) {
  if (newUuid === selectedUuid.value) return;
  if (await confirmAbandonChanges()) {
    selectedUuid.value = newUuid;
    // 清理旧状态的逻辑移到 watch 中
  }
}

async function handleAiTypeSelectionChange(newType: string | null) {
  if (newType === selectedAiModuleType.value) return;
  if (await confirmAbandonChanges()) {
    selectedAiModuleType.value = newType;
  }
}

function handleRemoveCurrentAiConfig() {
  if (!selectedAiModuleType.value || !currentConfigSet.value) return;

  // 这是本地修改，需要保存后才生效
  delete currentConfigSet.value.configurations[selectedAiModuleType.value];

  // 清理UI
  selectedAiModuleType.value = null;
  formDataCopy.value = null;
  currentSchema.value = null;

  formChanged.value = true; // 标记为需要保存
  message.info(`模型配置已移除。请点击“保存变更”以生效。`);
}

function checkFormChange() {
  formChanged.value = !isEqual(originalDataForCompare.value, formDataCopy.value);
}

// --- Watchers for State Transitions ---
watch(selectedUuid, (newUuid, oldUuid) => {
  if (newUuid === oldUuid) return;
  // 切换配置集时，重置AI类型和表单
  selectedAiModuleType.value = null;
  currentSchema.value = null;
  formDataCopy.value = null;
  originalDataForCompare.value = null;
  formChanged.value = false;
});

watch(selectedAiModuleType, async (newType) => {
  formChanged.value = false;

  if (!newType) {
    currentSchema.value = null;
    formDataCopy.value = null;
    return;
  }

  const schema = schemaStore.getSchemaByName(newType);
  if (!schema) {
    message.error(`未能找到模型 "${newType}" 的Schema。`);
    currentSchema.value = null;
    formDataCopy.value = null;
    return;
  }

  currentSchema.value = schema;

  // 从当前配置集中获取数据，如果不存在，则创建一个最小化的新对象
  const existingConfig = currentConfigSet.value?.configurations?.[newType];
  const data = existingConfig ? cloneDeep(existingConfig) : { configType: newType };

  formDataCopy.value = data as AbstractAiProcessorConfig;
  originalDataForCompare.value = cloneDeep(data); // 存储原始副本用于比较

  formRenderKey.value++; // 强制重新渲染表单
  await nextTick();
});

</script>

<style scoped>
.n-card { margin-bottom: 20px; }
.n-form-item { margin-bottom: 0; }
</style>