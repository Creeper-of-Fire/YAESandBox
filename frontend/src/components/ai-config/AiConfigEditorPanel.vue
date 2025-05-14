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
      <n-card v-if="currentConfigSet && selectedAiModuleType && currentSchema"
              :title="currentSchema.description">
        <n-spin :show="isCurrentSchemaLoading" description="正在加载模型 Schema...">
          <!-- Schema 加载错误 -->
          <div v-if="currentSchemaFetchError" style="padding: 20px;">
            <n-alert title="Schema 加载错误" type="error">
              {{ currentSchemaFetchError }}
            </n-alert>
          </div>
          <vue-form
              v-if="formRenderKey > 0 && typeof formDataCopy === 'object'"
              :key="formRenderKey"
              v-model="formDataCopy"
              :schema="currentSchema"
              :form-props="formGlobalProps"
              @change="checkFormChange"
              :form-footer={show:false}
              ref="jsonFormRef"
              style="flex-grow: 1;"
          >
          </vue-form>
          <n-empty v-else-if="!currentSchema" description="Schema 未加载或不存在。"/>
          <n-empty
              v-else-if="typeof formDataCopy !== 'object'"
              description="请先添加或配置此模型类型。"/>
        </n-spin>
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


// ----------- 全局状态与工具 -----------
const message = useMessage();
const dialog = useDialog();
const componentLoading = ref(0); // 活动API调用计数器
const jsonFormRef = ref<any>(null); // vue-form 实例引用
const schemaStore = useAiConfigSchemaStore();

// ----------- 配置集状态 -----------
// 使用 reactive 包裹 Record 本身，使其内部属性的增删也能被侦测
const allConfigSets = reactive<Record<string, AiConfigurationSet>>({});
// **新增**: 用于存储当前编辑表单的数据副本
const formDataCopy = ref<AbstractAiProcessorConfig | null>(null);
const selectedConfigSetUuid = ref<string | null>(null);
const formChanged = ref(false); // 标记当前表单是否有未保存的修改

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

// ----------- 自定义组件与表单 Props -----------
const MyCustomStringAutoComplete = markRaw(defineAsyncComponent(() => import('@/components/ai-config/MyCustomStringAutoComplete.vue'))); // 确保路径正确
const SliderWithInputWidget = markRaw(defineAsyncComponent(() => import('@/components/ai-config/SliderWithInputWidget.vue')))

const formGlobalProps = computed(() => ({
  // 这些是传递给 Naive UI NForm 组件的 props
  labelPlacement: 'top' as const,
  labelWidth: 'auto' as const,
  isMiniDes: true,
}));

/**
 * 预处理从后端获取的 JSON Schema，根据约定动态注入 ui:widget。
 * @param originalSchema 从后端获取的原始 JSON Schema 对象。
 * @returns 处理后、可供 vue-form 使用的 Schema 对象。
 */
function preprocessSchemaForWidgets(originalSchema: Record<string, any>): Record<string, any> {
  // 深拷贝原始 Schema，避免修改原始对象
  const schema = JSON.parse(JSON.stringify(originalSchema));

  if (schema.properties) {
    for (const fieldName in schema.properties) {
      const fieldProps = schema.properties[fieldName];

      // 确保每个 property 都有一个 ui:options 对象，方便后续写入
      if (!fieldProps['ui:options']) {
        fieldProps['ui:options'] = {};
      }
      // 也可以直接在 uiSchema 层面操作（如果 vue-form 优先 uiSchema）
      // 但既然你提议对 ui:widget 赋值，直接修改 fieldProps 里的 ui:widget 更直接

      // 规则1: 有 maximum 和 minimum -> SliderWidget
      if (fieldProps.type && (fieldProps.type === 'integer' || fieldProps.type === 'number')) {
        if (!fieldProps['ui:widget']) {
          if (fieldProps.hasOwnProperty('maximum') && fieldProps.hasOwnProperty('minimum')) {
            // 优先使用已有的 ui:widget (如果后端偶尔还是会传)，否则按约定赋值

            fieldProps['ui:options'].step = fieldProps['multipleOf'];
            fieldProps['ui:options'].default = fieldProps.default;
            fieldProps['ui:options'].max = fieldProps.maximum;
            fieldProps['ui:options'].min = fieldProps.minimum;
            if (fieldProps.type != 'integer')
              delete fieldProps['multipleOf'];
            fieldProps['ui:widget'] = SliderWithInputWidget; // 你需要确保 SliderWidget 已经注册或由库提供
          } else {
            fieldProps['ui:widget'] = 'InputNumberWidget';
            fieldProps['ui:options'].showButton = false;
          }
        }
      }

      // 规则2: 有 enum 和 enumNames
      if (fieldProps.enum && fieldProps.enumNames) {
        if (fieldProps['ui:options']?.isEditableSelectOptions === true) {
          if (!fieldProps['ui:widget']) { // 同样，优先用户已定义的
            fieldProps['ui:widget'] = MyCustomStringAutoComplete;
          }
        } else {
          if (!fieldProps['ui:widget']) {
            // 根据选项数量决定使用 Radio 还是 Select 可能是更好的实践
            // 但按你的约定，这里是 RadioWidget
            fieldProps['ui:widget'] = 'RadioWidget'; // 你需要确保 RadioWidget 已经注册或由库提供
          }
        }
      }
      // 你可以根据需要添加更多规则...

      // 清理临时的 isEditableSelectOptions，因为它只是一个标记，不应直接传递给 widget
      if (fieldProps['ui:options']?.hasOwnProperty('isEditableSelectOptions')) {
        // delete fieldProps['ui:options'].isEditableSelectOptions; // 看 vue-form 是否会把所有 ui:options 传给 widget
        fieldProps['ui:enumOptions'] = fieldProps['enum'].map((value: any, index: number) => ({
          label: fieldProps.enumNames[index] as string,
          value
        })) as Array<{ label: string, value: any }>;
        delete fieldProps['enum'];
        // 如果不删除，确保你的 MyCustomStringAutoComplete 和 RadioWidget 不会错误地使用这个标记
        // 通常，自定义 widget 会接收整个 ui:options 对象，所以如果标记只用于选择 widget，之后可以删除
      }
      // 如果 ui:options 为空，也可以考虑删除它
      if (Object.keys(fieldProps['ui:options']).length === 0) {
        delete fieldProps['ui:options'];
      }
    }
  }

  // 如果 Schema 中有 definitions (用于 $ref)，也可能需要递归处理它们
  // 但对于 ui:widget，通常只关心顶层 properties
  if (schema.definitions) {
    for (const defName in schema.definitions) {
      // 注意：这里递归调用 preprocessSchemaForWidgets(schema.definitions[defName])
      // 需要确保返回的是处理后的 definitions，并且原 schema 的 $ref 仍然有效。
      // 或者，一种更简单的方式是假设 ui:widget 仅应用于 schema.properties 中的字段，
      // 而不是 definitions 内部的字段。
      // 如果 definitions 内部的字段也需要通过 $ref 被渲染并应用这些规则，
      // 那么 vue-form 在解析 $ref 时，如果能应用外层传递的 widgets 映射，就无需递归。
      // 否则，递归处理 definitions 是必要的。
      // 为简单起见，我们先假设主要处理 schema.properties
      schema.definitions[defName] = preprocessSchemaForWidgets(schema.definitions[defName]);
    }
  }


  return schema;
}

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

// 保存对当前配置集的所有变更 (集成了校验)
async function handleSaveConfigSet() {
  if (!currentConfigSet.value || !selectedConfigSetUuid.value) {
    message.error('没有选中的配置集可以保存！');
    return;
  }
  if (!selectedAiModuleType.value || !currentSchema.value) {
    // 如果当前没有选中的 AI 类型或 Schema，可能只是修改了配置集名称
    // 这种情况下，如果 formChanged 为 true 是因为名称修改，则直接保存
    // 但如果 formChanged 是因为某个AI类型的表单被修改过，但现在这个AI类型未被选中，则行为需要明确
    // 为了简单起见，我们假设“保存变更”主要针对当前显示的表单和配置集本身
    // 如果 formChanged 是 true，但没有 active form，则可能只保存配置集元数据（如名称）
  }

  // 1. 手动触发表单校验
  if (jsonFormRef.value && jsonFormRef.value.$$uiFormRef) { // 确保表单实例和内部UI Form实例存在
    try {
      // 调用 Naive UI NForm 实例的 validate 方法
      // $$uiFormRef 指向的是 Naive UI 的 NForm 实例
      await jsonFormRef.value.$$uiFormRef.validate();
      // 如果没有抛出错误，则校验通过
    } catch (errors: any) {
      // errors 是一个包含校验失败信息的数组 (Naive UI 的格式)
      // vue-form 通常会自动在界面上显示错误信息
      message.error('表单校验失败，请检查红色标记的字段。');
      console.warn('表单校验失败:', errors);
      return; // 校验失败，不继续保存
    }
  } else if (selectedAiModuleType.value && currentSchema.value) {
    // 如果有选中的AI类型和Schema，但 jsonFormRef 或 $$uiFormRef 不可用（理论上不应发生）
    // 这是一个警告，说明可能存在问题，但我们也可以选择跳过校验或提示用户
    console.warn('无法访问表单实例进行校验，将不经验证地保存。');
    // 或者 message.warning('无法触发表单校验，请谨慎保存。'); return;
  }
  // 如果 selectedAiModuleType.value 为 null，说明当前没有活动的表单，无需校验。

  if (selectedAiModuleType.value && currentConfigSet.value && formDataCopy.value !== null) {
    // cloneDeep(formDataCopy.value) 的结果也符合 AbstractAiProcessorConfig
    currentConfigSet.value.configurations[selectedAiModuleType.value] = cloneDeep(formDataCopy.value);
    resetFormChangeFlag();
  }

  // 2. 调用 API 保存
  await callApi(() => AiConfigurationsService.putApiAiConfigurations({
    uuid: selectedConfigSetUuid.value!,
    requestBody: currentConfigSet.value!, // 包含所有已修改的数据
  }), '配置集保存成功！');
}

// 提示并执行新建配置集
function promptCreateNewSet() {
  const newSetNameInput = ref('');
  dialog.create({
    title: '新建 AI 配置集',
    content: () => h(NInput, {
      value: newSetNameInput.value,
      onUpdateValue: (val) => newSetNameInput.value = val,
      placeholder: '请输入新配置集的名称',
      autofocus: true,
    }),
    positiveText: '创建',
    negativeText: '取消',
    onPositiveClick: async () => {
      const name = newSetNameInput.value.trim();
      if (!name) {
        message.error('配置集名称不能为空！');
        return false; //阻止对话框关闭
      }
      const newSetToCreate: AiConfigurationSet = {
        configSetName: name,
        configurations: {},
      };
      const newUuid = await callApi(() => AiConfigurationsService.postApiAiConfigurations({requestBody: newSetToCreate}), `配置集 "${name}" 创建成功!`);
      if (newUuid) {
        await fetchAllConfigSets(); // 重新加载所有配置集
        selectedConfigSetUuid.value = newUuid; // 自动选中新创建的
      }
    }
  });
}

// 提示并执行复制当前配置集
function promptCloneSet() {
  if (!currentConfigSet.value) return;
  const clonedNameInput = ref(`${currentConfigSet.value.configSetName} (副本)`);
  dialog.create({
    title: '复制配置集',
    content: () => h(NInput, {
      value: clonedNameInput.value,
      onUpdateValue: (val) => clonedNameInput.value = val,
      placeholder: '请输入副本配置集的名称'
    }),
    positiveText: '复制',
    negativeText: '取消',
    onPositiveClick: async () => {
      const name = clonedNameInput.value.trim();
      if (!name) {
        message.error('配置集名称不能为空！');
        return false;
      }
      const setToClone: AiConfigurationSet = {
        configSetName: name,
        configurations: JSON.parse(JSON.stringify(currentConfigSet.value!.configurations)) // 深拷贝
      };
      const newUuid = await callApi(() => AiConfigurationsService.postApiAiConfigurations({requestBody: setToClone}), `配置集 "${name}" (副本) 创建成功!`);
      if (newUuid) {
        await fetchAllConfigSets();
        selectedConfigSetUuid.value = newUuid;
      }
    }
  });
}

// 提示并执行修改当前配置集名称
function promptRenameSet() {
  if (!currentConfigSet.value || !selectedConfigSetUuid.value) return;
  const newNameInput = ref(currentConfigSet.value.configSetName);
  const originalUuid = selectedConfigSetUuid.value; // 捕获当前uuid

  dialog.create({
    title: '修改配置集名称',
    content: () => h(NInput, {
      value: newNameInput.value,
      onUpdateValue: (val) => newNameInput.value = val,
      placeholder: '请输入新的配置集名称'
    }),
    positiveText: '保存名称',
    negativeText: '取消',
    onPositiveClick: async () => {
      const name = newNameInput.value.trim();
      if (!name) {
        message.error('配置集名称不能为空！');
        return false;
      }
      if (name === allConfigSets[originalUuid]?.configSetName) { // 名称未改变
        return;
      }

      // Optimistic UI update or prepare for save
      // Here, we choose to update and mark for "Save Changes"
      // or, if you want immediate save:
      const setToUpdate: AiConfigurationSet = {
        ...allConfigSets[originalUuid], // 获取最新的数据
        configSetName: name,
      };

      await callApi(() => AiConfigurationsService.putApiAiConfigurations({
        uuid: originalUuid,
        requestBody: setToUpdate
      }), `配置集名称已修改为 "${name}"`);

      // Manually update the name in the local reactive store for immediate UI feedback
      if (allConfigSets[originalUuid]) {
        allConfigSets[originalUuid].configSetName = name;
      }
      // If the currentConfigSet is the one being renamed, its name will update reactively.
      // No need to re-fetch all, just update local state.
    }
  });
}

// 执行删除当前配置集
async function executeDeleteSet() {
  if (!selectedConfigSetUuid.value || !currentConfigSet.value) return;
  const setName = currentConfigSet.value.configSetName; // 保存名称用于提示
  await callApi(() => AiConfigurationsService.deleteApiAiConfigurations({uuid: selectedConfigSetUuid.value!}));

  message.success(`配置集 "${setName}" 已删除!`);
  // 后端保证至少有一个配置集，但如果删除的是最后一个，前端需要有合理行为
  // 通常是重新拉取列表，如果列表空了，UI应有提示
  const oldSelectedUuid = selectedConfigSetUuid.value;
  selectedConfigSetUuid.value = null; // 清空选择
  selectedAiModuleType.value = null;
  currentSchema.value = null;

  // 从 allConfigSets 中移除已删除的项
  if (allConfigSets[oldSelectedUuid]) {
    delete allConfigSets[oldSelectedUuid];
  }
  // 如果 allConfigSets 为空，或者需要选择一个默认项，可以在这里处理
  // 考虑到后端保证至少有一个，这里可能不需要特殊处理空列表的情况
  // 但如果删除了当前选中的，最好是清空选择，或者选中列表中的第一个（如果存在）
  if (Object.keys(allConfigSets).length > 0 && !selectedConfigSetUuid.value) {
    // selectedConfigSetUuid.value = Object.keys(allConfigSets)[0]; // 可选：默认选中第一个
  } else if (Object.keys(allConfigSets).length === 0) {
    // 如果删到空了（理论上后端不应允许），这里可以再次拉取确保同步
    await fetchAllConfigSets();
  }
}

// ----------- AI 类型与表单逻辑 -----------
/**
 * 删除当前选中的 AI 配置
 */
async function handleRemoveCurrentAiConfig() {
  // 可用性已在模板 popconfirm 的 v-if 和 disabled 中控制，此处不再需要额外的 if 检查参数有效性
  const moduleTypeToRemove = selectedAiModuleType.value!;
  const currentConfigSetData = currentConfigSet.value!; // 已确保存在

  if (currentConfigSetData.configurations && currentConfigSetData.configurations[moduleTypeToRemove]) {
    // **核心修改：删除对应的键值对**
    delete currentConfigSetData.configurations[moduleTypeToRemove];

    message.info(`模型 "${moduleTypeToRemove}" 的配置已从配置集中移除。请点击“保存变更”以应用。`);
    selectedAiModuleType.value = null;

    // 删除后，当前选中的 AI 类型实际上已经没有对应的配置数据了
    // 如果希望 UI 立即反映移除状态（表单消失），可以清空 selectedAiModuleType
    // selectedAiModuleType.value = null; // 可选，取决于交互流程
    // 清空后需要重新触发选择AI类型或显示空状态
    // 如果不清空，表单区域也会因为 v-if 条件不满足而消失
    // 为了避免 watch selectedAiModuleType 再次触发加载 schema/数据，不清空可能更好
    // 让表单区域消失，并提示用户选择其他AI类型

    // 强制 vue-form 重新渲染通常不再需要，因为 v-if 条件变化会触发其销毁和重建（如果下次再选中）
    // formRenderKey.value++; // 移除或保留看实际测试效果
  } else {
    // 理论上不会走到这里，因为按钮已禁用
    message.warning('当前AI模型配置不存在或已被移除。');
  }
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
  console.timeEnd(`[Schema Perf] Get/Fetch Schema: ${newModuleType}`);
  if (!rawSchema || schemaStore.getSchemaError(newModuleType)) {
    currentSchema.value = null;
    formDataCopy.value = null;
    message.error(`加载模型 "${newModuleType}" 的 Schema 失败。`);
    return
  }
  // --- 在这里使用预处理器 ---
  console.time(`[Schema Perf] Preprocess Schema: ${newModuleType}`);
  currentSchema.value = preprocessSchemaForWidgets(rawSchema);
  if (!currentSchema.value["ui:options"]) {
    currentSchema.value["ui:options"] = {}
  }
  currentSchema.value["ui:options"].showTitle = false;
  currentSchema.value["ui:options"].showDescription = false;
  console.log(`[Schema Perf] Preprocess RawSchema:`, currentSchema.value);
  console.timeEnd(`[Schema Perf] Preprocess Schema: ${newModuleType}`);
  // -------------------------
  console.time(`[Schema Perf] State Update & Tick: ${newModuleType}`);
  if (!currentConfigSet.value.configurations) {
    currentConfigSet.value.configurations = reactive({});
  }
  let originalConfig = currentConfigSet.value.configurations[newModuleType];
  if (!originalConfig) {
    const initialData: AbstractAiProcessorConfig = {};
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