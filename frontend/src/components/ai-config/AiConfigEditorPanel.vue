<template>
  <n-spin :show="loading > 0">
    <n-space vertical size="large">
      <!-- 上方控制区域：配置集选择与操作 -->
      <n-card title="AI 配置集管理">
        <n-space align="center" justify="space-between" style="margin-bottom: 16px;">
          <n-form-item label="选择配置集" style="min-width: 300px; margin-bottom: 0;">
            <n-select
                v-model:value="selectedConfigSetUuid"
                :options="configSetOptions"
                placeholder="请选择一个配置集"
                clearable
                @update:value="handleConfigSetSelectionChange"
            />
          </n-form-item>
          <n-space>
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
          </n-space>
        </n-space>
      </n-card>

      <!-- 中间区域：AI 类型选择 -->
      <n-card v-if="currentConfigSet" title="AI 模型配置">
        <n-form-item label="选择或添加 AI 模型类型" style="min-width: 300px;">
          <n-select
              v-model:value="selectedAiModuleType"
              :options="aiTypeOptionsWithMarker"
              placeholder="选择 AI 模型类型"
              clearable
              @update:value="handleAiTypeSelectionChange"
              :disabled="availableAiTypes.length === 0"
          />
          <div v-if="availableAiTypes.length === 0 && loading === 0" style="color: grey; font-size: 12px; margin-top: 4px;">
            未能加载到可用的AI模型类型。
          </div>
        </n-form-item>
        <n-popconfirm
            v-if="selectedAiModuleType && currentConfigSet?.configurations?.[selectedAiModuleType]"
            @positive-click="handleClearCurrentAiConfig"
        >
          <template #trigger>
            <n-button type="warning" tertiary
                      :disabled="!selectedAiModuleType || !currentConfigSet?.configurations?.[selectedAiModuleType]">
              清空当前模型配置
            </n-button>
          </template>
          确定要清空当前模型 "{{ selectedAiModuleType }}" 的所有配置项吗？
          此操作会将其恢复到默认状态，但需要点击“保存变更”才会生效。
        </n-popconfirm>
      </n-card>

      <!-- 下方区域：动态表单 -->
      <n-card v-if="currentConfigSet && selectedAiModuleType && currentSchema"
              :title="`配置模型: ${selectedAiModuleType}`">
        <vue-form
            v-if="formRenderKey > 0 && currentConfigSet.configurations && typeof currentConfigSet.configurations[selectedAiModuleType] === 'object'"
            :key="formRenderKey"
            v-model="currentConfigSet.configurations[selectedAiModuleType]"
            :schema="currentSchema"
            :form-props="formGlobalProps"
            @change="onFormChange"
            :form-footer={show:false}
            ref="jsonFormRef"
        >
        </vue-form>
        <n-empty v-else-if="loading > 0 && !currentSchema" description="正在加载模型 Schema..."/>
        <n-empty v-else description="表单数据或Schema准备中..."/>
      </n-card>

      <n-empty v-if="!selectedConfigSetUuid && Object.keys(allConfigSets).length > 0 && loading === 0" description="请先选择一个AI配置集。"/>
      <n-empty v-else-if="Object.keys(allConfigSets).length === 0 && loading === 0" description="暂无配置集，请新建一个。"/>
      <n-empty v-else-if="currentConfigSet && !selectedAiModuleType && loading === 0"
               description="请选择一个AI模型类型进行配置。"/>

    </n-space>
  </n-spin>
</template>

<script setup lang="ts">
import {computed, defineAsyncComponent, h, markRaw, nextTick, onMounted, reactive, ref} from 'vue';
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
import {AiConfigurationsService} from '@/types/generated/aiconfigapi/services/AiConfigurationsService';
import {AiConfigSchemasService} from '@/types/generated/aiconfigapi/services/AiConfigSchemasService';
import type {AiConfigurationSet} from '@/types/generated/aiconfigapi/models/AiConfigurationSet';
import type {SelectOption as ApiSelectOption} from '@/types/generated/aiconfigapi/models/SelectOption';
import type {AbstractAiProcessorConfig} from "@/types/generated/aiconfigapi/models/AbstractAiProcessorConfig"; // 虽然字段少了，但类型可能仍被引用


// ----------- 全局状态与工具 -----------
const message = useMessage();
const dialog = useDialog();
const loading = ref(0); // 活动API调用计数器
const jsonFormRef = ref<any>(null); // vue-form 实例引用

// ----------- 配置集状态 -----------
// 使用 reactive 包裹 Record 本身，使其内部属性的增删也能被侦测
const allConfigSets = reactive<Record<string, AiConfigurationSet>>({});
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

const formGlobalProps = computed(() => ({
  // 这些是传递给 Naive UI NForm 组件的 props
  labelPlacement: 'top' as const,
  labelWidth: 'auto' as const,
  isMiniDes: true,
  // submitButton: false, // @lljj/vue3-form-naive 可能有自己的提交按钮控制
  // hideSubmitButton: true, // 或类似的方式隐藏默认提交按钮
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
      if (fieldProps.hasOwnProperty('maximum') && fieldProps.hasOwnProperty('minimum') && fieldProps.type && (fieldProps.type === 'integer' || fieldProps.type === 'number')) {
        // 优先使用已有的 ui:widget (如果后端偶尔还是会传)，否则按约定赋值
        if (!fieldProps['ui:widget']) {
          fieldProps['ui:widget'] = 'SliderWidget'; // 你需要确保 SliderWidget 已经注册或由库提供
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
  loading.value++;
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
    loading.value--;
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

const canClearCurrentAiConfig = computed(() => {
  return !!(selectedAiModuleType.value &&
      currentConfigSet.value &&
      currentConfigSet.value.configurations &&
      currentConfigSet.value.configurations[selectedAiModuleType.value]);
});

async function handleClearCurrentAiConfig() {
  if (!canClearCurrentAiConfig.value) {
    message.warning('没有可清空的当前AI模型配置。');
    return;
  }

  const moduleTypeToClear = selectedAiModuleType.value!; // 已经用 canClearCurrentAiConfig 保证了非 null

  // 选项 A: 重置为空对象，让 vue-form 根据 schema default 填充
  if (currentConfigSet.value!.configurations[moduleTypeToClear]) {
    // 为了触发 vue-form 的重新计算和基于 default 的填充，
    // 我们不能简单地 currentConfigSet.value.configurations[moduleTypeToClear] = {};
    // 因为 vue-form 可能已经持有对原对象的引用。
    // 更好的方式是，如果 vue-form 的 v-model 绑定的是一个对象的属性，
    // 我们可以清空该对象的所有键，或者用一个全新的空对象替换它。
    // 如果 v-model 的目标是 currentConfigSet.value.configurations[moduleTypeToClear] 本身，
    // 那么将其重置为 {} 是可以的。

    // 遍历并删除所有属性，或者直接替换为一个新的响应式空对象
    // currentConfigSet.value!.configurations[moduleTypeToClear] = reactive({});

    // 或者，如果想确保 vue-form 完全重新处理，可以先删除再添加回去
    // (但要注意这可能导致 vue-form 内部状态丢失，如果它有的话)
    // delete currentConfigSet.value!.configurations[moduleTypeToClear];
    // await nextTick(); // 给 Vue 一个周期去处理删除
    // currentConfigSet.value!.configurations[moduleTypeToClear] = reactive({});

    // 最稳妥的方式，如果 vue-form 内部对 v-model 传入对象的引用敏感：
    // 创建一个新的空对象，并让父级 configurations 对象知道这个 moduleType 的值变了。
    // 如果 currentConfigSet.configurations 本身是 reactive 的，
    // 那么对其属性的直接赋值 (替换对象) 应该是能被侦测到的。
    currentConfigSet.value!.configurations[moduleTypeToClear] = reactive({});


    message.info(`模型 "${moduleTypeToClear}" 的配置已清空至默认值。请点击“保存变更”以应用。`);
    formChanged.value = true;

    // 强制 vue-form 重新渲染，以应用 schema 的 default 值
    // (如果 vue-form 在 v-model 的值从有数据对象变为 {} 时，能自动应用 default)
    // 递增 formRenderKey 通常是一个可靠的强制刷新方法
    formRenderKey.value++;
    await nextTick(); // 等待DOM更新

  } else {
    message.error('无法找到要清空的配置数据。'); // 理论上不应发生，因为有 canClearCurrentAiConfig 控制
  }
}


// 封装放弃更改的确认逻辑
async function confirmAbandonChanges(title: string, content: string): Promise<boolean> {
  if (!formChanged.value) return true; // 没有更改，直接允许
  return new Promise<boolean>(resolve => {
    dialog.warning({
      title,
      content,
      positiveText: '确定放弃',
      negativeText: '取消操作',
      onPositiveClick: () => resolve(true),
      onNegativeClick: () => resolve(false),
      onClose: () => resolve(false),
    });
  });
}

// 当配置集选择变化时
async function handleConfigSetSelectionChange(uuid: string | null) {
  const oldUuid = selectedConfigSetUuid.value;
  if (uuid === oldUuid) return;

  if (oldUuid) { // 如果之前有选中的项
    const canProceed = await confirmAbandonChanges(
        '放弃未保存的更改？',
        `配置集 "${allConfigSets[oldUuid]?.configSetName}" 有未保存的更改。切换配置集将丢失这些更改。`
    );
    if (!canProceed) {
      await nextTick(); // 等待 select 组件状态回滚
      selectedConfigSetUuid.value = oldUuid; // 恢复原选择
      return;
    }
  }

  selectedConfigSetUuid.value = uuid; // 更新选择
  resetFormChangeFlag(); // 重置表单修改标记
  selectedAiModuleType.value = null; // 清空AI类型选择
  currentSchema.value = null;      // 清空当前Schema
  formRenderKey.value = 0;         // 重置表单渲染key
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

  // 2. 调用 API 保存
  await callApi(() => AiConfigurationsService.putApiAiConfigurations({
    uuid: selectedConfigSetUuid.value!,
    requestBody: currentConfigSet.value!, // 包含所有已修改的数据
  }), '配置集保存成功！');
  resetFormChangeFlag();
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
  resetFormChangeFlag();

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
const aiTypeOptionsWithMarker = computed<NaiveSelectOption[]>(() => {
  return availableAiTypes.value.map(type => {
    const isConfigured = !!(currentConfigSet.value?.configurations && currentConfigSet.value.configurations[type.value as string]);
    return {
      label: `${type.label}${isConfigured ? ' ✔️' : ''}`,
      value: type.value as string, // 确保 value 是 string
    };
  });
});

// 当AI模型类型选择变化时
async function handleAiTypeSelectionChange(moduleType: string | null) {
  const oldModuleType = selectedAiModuleType.value;
  if (moduleType === oldModuleType && currentSchema.value) return;

  if (oldModuleType && currentConfigSet.value?.configurations?.[oldModuleType]) {
    const canProceed = await confirmAbandonChanges(
        '放弃未保存的更改？',
        `AI模型 "${oldModuleType}" 的配置有未保存的更改。切换类型将丢失这些更改。`
    );
    if (!canProceed) {
      await nextTick();
      selectedAiModuleType.value = oldModuleType;
      return;
    }
  }
  resetFormChangeFlag();
  selectedAiModuleType.value = moduleType;

  if (moduleType && currentConfigSet.value) {
    const rawSchema = await callApi(() => AiConfigSchemasService.getApiAiConfigurationManagementSchemas({configTypeName: moduleType}));
    if (rawSchema) {
      // --- 在这里使用预处理器 ---
      currentSchema.value = preprocessSchemaForWidgets(rawSchema);
      // -------------------------

      if (!currentConfigSet.value.configurations) {
        currentConfigSet.value.configurations = reactive({});
      }
      if (!currentConfigSet.value.configurations[moduleType]) {
        const initialData: AbstractAiProcessorConfig = {};
        // 让 vue-form 根据（可能已预处理过的）schema 的 default 自动填充
        currentConfigSet.value.configurations[moduleType] = reactive(initialData);
        formChanged.value = true;
      }
      formRenderKey.value++;
      await nextTick();

    } else {
      currentSchema.value = null;
      message.error(`加载模型 "${moduleType}" 的 Schema 失败。`);
    }
  } else {
    currentSchema.value = null;
  }
}

function onFormChange() {
  formChanged.value = true;
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