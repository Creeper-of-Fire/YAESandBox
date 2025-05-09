<template>
  <n-spin :show="isLoadingOverall">
    <n-card :title="computedPanelTitle" :bordered="false" style="width: 100%;">
      <n-alert v-if="errorMessage" title="发生错误" type="error" closable @close="errorMessage = null">
        {{ errorMessage }}
      </n-alert>

      <!-- 创建模式：选择配置类型 -->
      <n-form-item v-if="mode === 'create'" label="选择配置类型" required :show-feedback="!selectedConfigType && formSubmittedOnce">
        <n-select
            v-model:value="selectedConfigType"
            placeholder="请选择要创建的 AI 配置类型"
            :options="availableConfigTypes"
            :loading="isLoadingConfigTypes"
            :disabled="isLoadingSchema"
            filterable
            clearable
            @update:value="handleConfigTypeChange"
        />
      </n-form-item>

      <!-- 表单渲染区域 -->
      <div v-if="currentSchema && currentSchema.length > 0" style="margin-top: 16px;"
           :key="mode === 'create' ? (selectedConfigType||undefined) : props.configUuid || 'form-wrapper'">
        <SchemaDrivenForm
            ref="schemaFormRef"
            :schema="currentSchema"
            v-model:modelValue="formData"
            :disabled="isSubmitting || isLoadingSchema"
            @update:modelValue="handleFormUpdate"
            :key="mode === 'create' ? (selectedConfigType || 'create-form') : (props.configUuid || 'edit-form')"
        />
      </div>
      <!-- 这个 n-empty 的条件也需要调整，或者暂时先去掉，确保 SchemaDrivenForm 能渲染 -->
      <n-empty v-else-if="mode === 'create' && !selectedConfigType && !isLoadingOverall && (!currentSchema || currentSchema.length === 0)"
               description="请先选择配置类型以加载表单。" style="margin-top: 20px;" key="empty-create"/>
      <n-empty v-else-if="mode === 'edit' && (!currentSchema || currentSchema.length === 0) && !isLoadingOverall"
               description="无法加载配置表单结构。" style="margin-top: 20px;" key="empty-edit"/>


      <!-- 操作按钮 -->
      <n-space justify="end" style="margin-top: 24px;">
        <n-button v-if="showCancelButton" @click="handleCancel" :disabled="isSubmitting">
          取消
        </n-button>
        <n-button
            v-if="mode === 'edit' && showDeleteButton && configUuid"
            type="error"
            ghost
            @click="confirmDelete"
            :loading="isDeleting"
            :disabled="isSubmitting"
        >
          删除
        </n-button>
        <n-button
            type="primary"
            @click="handleSubmit"
            :loading="isSubmitting"
            :disabled="isLoadingSchema || !canSubmit"
        >
          {{ mode === 'create' ? '创建配置' : '保存更改' }}
        </n-button>
      </n-space>
    </n-card>
  </n-spin>
</template>

<script setup lang="ts">
import {computed, onMounted, ref, watch} from 'vue';
import {
  NAlert,
  NButton,
  NCard,
  NEmpty,
  NFormItem,
  NSelect,
  NSpace,
  NSpin,
  type SelectOption as NaiveSelectOption,
  useDialog,
  useMessage,
} from 'naive-ui';
import {
  type AbstractAiProcessorConfig,
  AiConfigSchemasService,
  AiConfigurationsService,
  type FormFieldSchema,
  type SelectOption as ApiSelectOption,
} from '@/types/generated/aiconfigapi';
import type SchemaDrivenForm from "@/components/schema/SchemaDrivenForm.vue"; // 假设你的类型文件路径

// --- Props ---
const props = defineProps({
  configUuid: { // 用于编辑模式，如果提供，则进入编辑模式
    type: String,
    default: null,
  },
  panelTitle: { // 可选的面板标题
    type: String,
    default: '',
  },
  showCancelButton: { // 是否显示取消按钮
    type: Boolean,
    default: true,
  },
  showDeleteButton: { // (仅编辑模式) 是否显示删除按钮
    type: Boolean,
    default: true,
  },
});

// --- Emits ---
const emit = defineEmits<{
  (e: 'config-created', uuid: string, data: AbstractAiProcessorConfig): void;
  (e: 'config-updated', uuid: string, data: AbstractAiProcessorConfig): void;
  (e: 'config-deleted', uuid: string): void;
  (e: 'cancel'): void;
  (e: 'error', message: string): void;
}>();

// --- Naive UI 钩子 ---
const message = useMessage();
const dialog = useDialog();

// --- 组件内部状态 ---
const mode = ref<'create' | 'edit'>(props.configUuid ? 'edit' : 'create');
const isLoadingConfigTypes = ref(false); // 加载可用配置类型的状态
const isLoadingSchema = ref(false);      // 加载 Schema 的状态
const isLoadingData = ref(false);        // 加载配置数据的状态 (编辑模式)
const isSubmitting = ref(false);         // 提交表单时的状态
const isDeleting = ref(false);           // 删除配置时的状态
const formSubmittedOnce = ref(false);    // 标记表单是否至少尝试提交过一次，用于校验提示

const availableConfigTypes = ref<NaiveSelectOption[]>([]); // Naive UI Select 的选项
const selectedConfigType = ref<string | null>(null);     // 当前选择的AI配置类型名称 (例如 "DoubaoAiProcessorConfig")
const currentSchema = ref<FormFieldSchema[] | null>(null);
const formData = ref<Record<string, any>>({});
const schemaFormRef = ref<InstanceType<typeof SchemaDrivenForm> | null>(null);
const initialFormDataForEdit = ref<AbstractAiProcessorConfig | null>(null); // 编辑模式下原始数据，用于比较

const errorMessage = ref<string | null>(null);

// --- 计算属性 ---
const isLoadingOverall = computed(() => {
  return isLoadingConfigTypes.value || isLoadingSchema.value || isLoadingData.value;
});

const computedPanelTitle = computed(() => {
  if (props.panelTitle) return props.panelTitle;
  return mode.value === 'create' ? '创建新的 AI 配置' : '编辑 AI 配置';
});

// 提交按钮是否可用的条件
const canSubmit = computed(() => {
  if (mode.value === 'create' && !selectedConfigType.value) {
    return false; // 创建模式下必须选择类型
  }
  if (!currentSchema.value || Object.keys(formData.value).length === 0) {
    return false; // Schema 或表单数据未就绪
  }
  return true;
});


// --- 方法 ---

/**
 * 初始化组件，根据模式加载必要数据
 */
async function initializeComponent() {
  errorMessage.value = null;
  if (mode.value === 'create') {
    await fetchAvailableConfigTypes();
    // 如果只有一个可用类型，可以考虑自动选中它
    // if (availableConfigTypes.value.length === 1 && availableConfigTypes.value[0].value) {
    //   selectedConfigType.value = availableConfigTypes.value[0].value as string;
    //   await handleConfigTypeChange(selectedConfigType.value);
    // }
  } else if (mode.value === 'edit' && props.configUuid) {
    await fetchConfigDataAndSchema(props.configUuid);
  }
}

/**
 * 获取所有可用的 AI 配置类型列表
 */
async function fetchAvailableConfigTypes() {
  isLoadingConfigTypes.value = true;
  try {
    const types: ApiSelectOption[] = await AiConfigSchemasService.getApiAiConfigurationManagementAvailableConfigTypes();
    availableConfigTypes.value = types.map(t => ({label: t.label, value: String(t.value)})); // 确保 value 是 string
  } catch (error: any) {
    console.error('获取可用配置类型失败:', error);
    errorMessage.value = `获取可用配置类型失败: ${error.message || '未知错误'}`;
    emit('error', errorMessage.value);
  } finally {
    isLoadingConfigTypes.value = false;
  }
}

/**
 * 根据类型名称获取表单 Schema
 */
async function fetchSchema(configTypeName: string) {
  if (!configTypeName) {
    currentSchema.value = null;
    formData.value = {}; // 清空表单数据
    return;
  }
  isLoadingSchema.value = true;
  currentSchema.value = null; // 先清空，避免显示旧的 schema
  // formData.value = {}; // 清空旧表单数据
  try {
    currentSchema.value = await AiConfigSchemasService.getApiAiConfigurationManagementSchemas({configTypeName});
    // Schema 加载后，SchemaDrivenForm 内部会根据 schema 初始化 formData
    // 但我们需要确保 formData 被重置为一个新对象，以触发 SchemaDrivenForm 的 modelValue watcher
    // SchemaDrivenForm 内部会根据 schema 的 defaultValue 初始化
    const newFormData: Record<string, any> = {};
    // (SchemaDrivenForm现在内部处理默认值，这里主要确保对象引用变化)
    currentSchema.value.forEach(field => {
      // 这里可以预先根据 schema 的 defaultValue 构建一个初始的 formData
      // newFormData[field.name] = field.defaultValue !== undefined ? field.defaultValue : getDefaultValueForType(field.schemaDataType);
      // 但更好的方式是让 SchemaDrivenForm 自己处理，我们只传递一个空对象或已有的数据
    });
    formData.value = {...newFormData}; // 触发 SchemaDrivenForm 更新

  } catch (error: any) {
    console.error(`获取配置类型 "${configTypeName}" 的 Schema 失败:`, error);
    errorMessage.value = `获取表单结构失败: ${error.message || '未知错误'}`;
    emit('error', errorMessage.value);
    currentSchema.value = null;
    formData.value = {};
  } finally {
    isLoadingSchema.value = false;
    console.log('fetchSchema done:', {
      isLoadingSchema: isLoadingSchema.value,
      selectedConfigType: selectedConfigType.value,
      currentSchemaIsNull: currentSchema.value === null,
      currentSchemaLength: currentSchema.value?.length,
      formDataKeys: Object.keys(formData.value).length,
      canSubmit: canSubmit.value
    });
  }
}

/**
 * (编辑模式) 获取指定 UUID 的配置数据及其 Schema
 */
async function fetchConfigDataAndSchema(uuid: string) {
  isLoadingData.value = true;
  isLoadingSchema.value = true; // 同时标记 schema 也在加载（或依赖于数据加载）
  formData.value = {};
  currentSchema.value = null;
  initialFormDataForEdit.value = null;

  try {
    const configData = await AiConfigurationsService.getApiAiConfigurations1({uuid});
    initialFormDataForEdit.value = configData; // 保存原始数据
    selectedConfigType.value = configData.moduleType; // 从数据中确定类型

    // 现在我们有了 moduleType，可以去获取 schema
    await fetchSchema(configData.moduleType);

    // Schema 加载完成后，用获取到的 configData 填充 formData
    // 注意：SchemaDrivenForm 的 v-model 会处理这个
    // 我们需要确保 formData 是一个新对象，并且包含了 configData 的值
    // SchemaDrivenForm 的 watch(props.modelValue) 会处理初始填充
    formData.value = {...configData};


  } catch (error: any) {
    console.error(`获取配置数据 (UUID: ${uuid}) 失败:`, error);
    errorMessage.value = `加载配置数据失败: ${error.message || '未知错误'}`;
    emit('error', errorMessage.value);
  } finally {
    isLoadingData.value = false;
    isLoadingSchema.value = false; // 确保 schema 加载状态也解除
  }
}

/**
 * 当用户在创建模式下选择不同配置类型时的处理
 */
async function handleConfigTypeChange(configTypeName: string | null) {
  selectedConfigType.value = configTypeName; // 更新内部状态
  if (configTypeName) {
    await fetchSchema(configTypeName);
  } else {
    currentSchema.value = null;
    formData.value = {};
  }
}

/**
 * 内部 SchemaDrivenForm 的 modelValue 更新时
 */
function handleFormUpdate(newFormData: Record<string, any>) {
  // formData.value = newFormData; // v-model 已经处理了
  // console.log('Form data updated in panel:', newFormData);
}

/**
 * 处理表单提交（创建或更新）
 */
async function handleSubmit() {
  formSubmittedOnce.value = true;
  if (!schemaFormRef.value) {
    errorMessage.value = '表单引用未准备好。';
    return;
  }

  try {
    await schemaFormRef.value.validate(); // 调用 SchemaDrivenForm 的校验方法
    // 校验通过
    isSubmitting.value = true;
    errorMessage.value = null;

    // 准备提交的数据
    // SchemaDrivenForm 返回的 formData 已经是处理过的 (例如字典从数组转对象)
    const dataToSubmit: any = {...formData.value};

    if (mode.value === 'create') {
      if (!selectedConfigType.value) {
        message.error('内部错误：未选择配置类型，但尝试创建。');
        isSubmitting.value = false;
        return;
      }
      dataToSubmit.ModuleType = selectedConfigType.value; // 关键：添加辨别器属性
      await handleCreate(dataToSubmit);
    } else if (mode.value === 'edit' && props.configUuid) {
      if (!initialFormDataForEdit.value?.moduleType) {
        message.error('内部错误：无法确定编辑配置的 ModuleType。');
        isSubmitting.value = false;
        return;
      }
      dataToSubmit.ModuleType = initialFormDataForEdit.value.moduleType; // 编辑时 ModuleType 通常不可变
      await handleUpdate(props.configUuid, dataToSubmit);
    }
  } catch (validationErrors: any) {
    // Naive UI 的 validate() 在失败时会 reject 一个包含错误信息的数组
    // 对于 SchemaDrivenForm, 它可能直接 reject 或我们让它 reject
    console.warn('表单校验失败:', validationErrors);
    message.warning('请检查表单输入项是否符合要求。');
    // errorMessage.value = '表单校验失败，请检查红色标记的字段。';
    // 可以在 SchemaDrivenForm 内部处理校验失败的UI提示
  } finally {
    isSubmitting.value = false;
  }
}

/**
 * 执行创建逻辑
 */
async function handleCreate(configData: any) {
  try {
    // configName 是必须的，但它应该已经在 formData 中由 schema 定义了
    // 确保 configName 存在 (如果 schema 没有定义 configName, 后端会报错)
    if (!configData.configName) {
      message.error('配置名称 (configName) 是必须的。请确保表单中包含此字段。');
      emit('error', '配置名称 (configName) 是必须的。');
      isSubmitting.value = false; // 确保重置
      return;
    }

    const createdUuid = await AiConfigurationsService.postApiAiConfigurations({requestBody: configData});
    message.success('AI 配置创建成功！');
    emit('config-created', createdUuid, configData as AbstractAiProcessorConfig);
    // 可以在这里重置表单或执行其他操作
    // resetFormForCreate();
  } catch (error: any) {
    console.error('创建 AI 配置失败:', error);
    const errorDetail = error?.body?.detail || error.message || '未知错误';
    errorMessage.value = `创建失败: ${errorDetail}`;
    emit('error', errorMessage.value);
  }
}

/**
 * 执行更新逻辑
 */
async function handleUpdate(uuid: string, configData: any) {
  try {
    // 确保 configName 存在 (如果 schema 没有定义 configName, 后端会报错)
    if (!configData.configName) {
      message.error('配置名称 (configName) 是必须的。请确保表单中包含此字段。');
      emit('error', '配置名称 (configName) 是必须的。');
      isSubmitting.value = false; // 确保重置
      return;
    }
    await AiConfigurationsService.putApiAiConfigurations({uuid, requestBody: configData});
    message.success('AI 配置更新成功！');
    initialFormDataForEdit.value = {...configData}; // 更新本地的 "原始" 数据
    emit('config-updated', uuid, configData as AbstractAiProcessorConfig);
  } catch (error: any) {
    console.error('更新 AI 配置失败:', error);
    const errorDetail = error?.body?.detail || error.message || '未知错误';
    errorMessage.value = `更新失败: ${errorDetail}`;
    emit('error', errorMessage.value);
  }
}

/**
 * 确认并执行删除逻辑
 */
function confirmDelete() {
  if (!props.configUuid) return;
  dialog.warning({
    title: '确认删除',
    content: `确定要删除配置 "${formData.value?.configName || props.configUuid}" 吗？此操作不可撤销。`,
    positiveText: '确定删除',
    negativeText: '取消',
    onPositiveClick: async () => {
      await handleDelete(props.configUuid!);
    },
  });
}

async function handleDelete(uuid: string) {
  isDeleting.value = true;
  try {
    await AiConfigurationsService.deleteApiAiConfigurations({uuid});
    message.success('AI 配置删除成功！');
    emit('config-deleted', uuid);
    // 删除成功后，可能需要关闭面板或导航到其他地方，由父组件处理
  } catch (error: any) {
    console.error('删除 AI 配置失败:', error);
    const errorDetail = error?.body?.detail || error.message || '未知错误';
    errorMessage.value = `删除失败: ${errorDetail}`;
    emit('error', errorMessage.value);
  } finally {
    isDeleting.value = false;
  }
}

/**
 * 处理取消操作
 */
function handleCancel() {
  // 可以询问用户是否放弃未保存的更改
  // if (hasChanges()) { ... }
  emit('cancel');
}

/**
 * 重置表单（用于创建成功后清空以便创建下一个）
 */
function resetFormForCreate() {
  selectedConfigType.value = null;
  currentSchema.value = null;
  formData.value = {};
  formSubmittedOnce.value = false;
  if (schemaFormRef.value) {
    schemaFormRef.value.restoreValidation(); // 清除校验状态
  }
  // 可以考虑是否重新获取可用类型，以防类型列表有变动
  // fetchAvailableConfigTypes();
}


// --- 生命周期钩子 ---
onMounted(() => {
  initializeComponent();
});

// --- 侦听器 ---
// 监听 configUuid prop 的变化，以支持在同一个组件实例中切换编辑目标或从创建切换到编辑
watch(() => props.configUuid, (newUuid, oldUuid) => {
  if (newUuid !== oldUuid) {
    mode.value = newUuid ? 'edit' : 'create';
    // 清理状态
    selectedConfigType.value = null;
    currentSchema.value = null;
    formData.value = {};
    initialFormDataForEdit.value = null;
    errorMessage.value = null;
    formSubmittedOnce.value = false;
    if (schemaFormRef.value) {
      schemaFormRef.value.restoreValidation();
    }
    initializeComponent(); // 重新初始化
  }
});

// 暴露方法给父组件 (如果需要)
// defineExpose({
//   submit: handleSubmit,
//   reset: mode.value === 'create' ? resetFormForCreate : () => { /* 编辑模式重置逻辑 */ }
// });

</script>

<style scoped>
/* 可以添加一些组件特定的样式 */
.n-card {
  max-width: 800px; /* 根据需要调整最大宽度 */
  margin: 0 auto; /* 居中显示 */
}
</style>