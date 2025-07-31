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
      <n-button size="small" @click="workflowsAsync.execute(0)">重试</n-button>
    </n-alert>
  </div>
</template>

<script lang="ts" setup>
import {computed, onMounted, ref} from 'vue';
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore';
import type {SelectOption} from 'naive-ui';
import {NAlert, NButton, NH5, NSelect} from 'naive-ui';
import type {WorkflowProcessorConfig} from "@/app-workbench/types/generated/workflow-config-api-client";

const emit = defineEmits(['workflow-selected']);

const workbenchStore = useWorkbenchStore();
const workflowsAsync = workbenchStore.globalWorkflowsAsync;
const workflowsIsLoading = computed(() => workflowsAsync.isLoading);
const workflowsError = computed(() => workflowsAsync.error as any);
const workflows = computed(() => workflowsAsync.state);

const selectedKey = ref<string | null>(null);

type SuccessWorkflowResourceItem = { isSuccess: true; data: WorkflowProcessorConfig };

const selectOptions = computed<SelectOption[]>(() =>
{
  if (!workflows.value) return [];
  return Object.entries(workflows.value)
      .filter((entry): entry is [string, SuccessWorkflowResourceItem] => entry[1]?.isSuccess === true)
      .map(([id, item]) => ({
        label: item.data.name,
        value: id,
      }));
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
  if (!workflowsAsync.isReady && !workflowsAsync.isLoading)
  {
    workflowsAsync.execute();
  }
});
</script>

<style scoped>
.workflow-selector-container {
  padding: 16px;
  border-bottom: 1px solid #e8e8e8;
}
</style>