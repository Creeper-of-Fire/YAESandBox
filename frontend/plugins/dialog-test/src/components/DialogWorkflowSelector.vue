<template>
  <div class="workflow-selector-container">
    <n-h5>选择工作流</n-h5>
    <n-select
        :disabled="workflowsIsLoading || !!workflowsError"
        :loading="workflowsIsLoading"
        :options="selectOptions"
        :value="selectedKey"
        filterable
        placeholder="请选择一个用于对话的工作流"
        @update:value="handleSelect"
    />
    <n-alert v-if="workflowsError" style="margin-top: 8px;" title="加载工作流失败" type="error">
      {{ workflowsError.message }}
      <n-button size="small" @click="workflowExecute()">重试</n-button>
    </n-alert>
  </div>
</template>

<script lang="ts" setup>
import {computed, inject, onMounted, ref} from 'vue';
import type {SelectOption} from 'naive-ui';
import {NAlert, NButton, NH5, NSelect} from 'naive-ui';
import {WorkflowConfigProviderKey} from "@yaesandbox-frontend/core-services/injectKeys";

const emit = defineEmits(['workflow-selected']);

const workflowConfigProvider = inject(WorkflowConfigProviderKey);
if (!workflowConfigProvider)
{
  // 如果在没有提供者的上下文中使用此组件，可以进行优雅降级或抛出错误
  throw new Error("[inject]workflowConfigProvider未提供");
}
const {
  state: workflows,
  isLoading: workflowsIsLoading,
  isReady: workflowsIsReady,
  error: workflowsError,
  execute: workflowExecute
} = workflowConfigProvider;

const selectedKey = ref<string | null>(null);

const selectOptions = computed<SelectOption[]>(() =>
{
  if (!workflows.value) return [];
  return Object.entries(workflows.value).reduce((acc, [id, item]) =>
  {
    // 使用简单的类型守卫
    if (item?.isSuccess)
    {
      // 在这个 if 代码块内部, TypeScript 知道 item 的类型是:
      // { isSuccess: true; data: WorkflowConfig; }
      // 同样，可以直接使用 item.data，因为它满足 RawWorkflowConfig 的结构
      acc.push({
        label: item.data.name,
        value: id,
      });
    }
    return acc;
  }, [] as SelectOption[]);
});

function handleSelect(key: string)
{
  const selected = workflows.value?.[key];
  if (selected && selected.isSuccess)
  {
    selectedKey.value = key;
    emit('workflow-selected', {id: key, config: selected.data});
  }
}

onMounted(() =>
{
  if (!workflowsIsReady && !workflowsIsLoading)
  {
    workflowExecute();
  }
});


</script>

<style scoped>
.workflow-selector-container {
  padding: 16px;
  border-bottom: 1px solid #e8e8e8;
}
</style>