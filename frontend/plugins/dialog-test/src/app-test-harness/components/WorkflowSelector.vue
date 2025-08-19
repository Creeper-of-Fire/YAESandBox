<template>
  <div>
    <n-h4>选择工作流</n-h4>
    <n-spin :show="workflowsIsLoading">
      <div v-if="workflowsError" class="error-state">
        <n-alert title="加载失败" type="error">
          无法加载工作流列表。
        </n-alert>
        <n-button block secondary strong style="margin-top: 12px;" @click="workflowsAsync.execute(0)">重试</n-button>
      </div>
      <n-menu
          v-else
          :options="menuOptions"
          :value="selectedKey"
          @update:value="handleSelect"
      />
    </n-spin>
  </div>
</template>

<script lang="ts" setup>
import {computed, h, onMounted, ref} from 'vue';
import {useWorkbenchStore} from '@yaesandbox-frontend/plugin-workbench/stores/workbenchStore.ts';
import type {MenuOption} from 'naive-ui';
import {NAlert, NButton, NH4, NIcon, NMenu, NSpin} from 'naive-ui';
import {WorkflowIcon} from '@yaesandbox-frontend/shared-ui/icons';
import type {WorkflowConfig} from "@yaesandbox-frontend/plugin-workbench/types/generated/workflow-config-api-client"; // 假设你有一个工作流图标

const workbenchStore = useWorkbenchStore();
const workflowsAsync = workbenchStore.globalWorkflowsAsync;
const workflowsIsLoading = computed(() => workflowsAsync.isLoading);
const workflowsError = computed(() => workflowsAsync.error);

const workflows = computed(() => workflowsAsync.state);

const emit = defineEmits(['workflow-selected']);

const selectedKey = ref<string | null>(null);

// 定义一个更具体的成功状态类型，用于类型谓词
type SuccessWorkflowResourceItem = { isSuccess: true; data: WorkflowConfig }

const menuOptions = computed<MenuOption[]>(() =>
{
  if (!workflows.value) return [];
  return Object.entries(workflows.value)
      // 使用类型谓词 (entry is [string, SuccessWorkflowResourceItem])
      // 来告诉 TypeScript，过滤后的数组条目类型是更具体的成功状态
      .filter(
          (entry): entry is [string, SuccessWorkflowResourceItem] => entry[1]?.isSuccess === true
      )
      // 现在，map 方法中的 item 类型被正确推断为 SuccessWorkflowResourceItem
      .map(([id, item]) => ({
        label: item.data.name, // 无需再用 '!' 断言
        key: id,
        icon: () => h(NIcon, {component: WorkflowIcon})
      }));
});

function handleSelect(key: string)
{
  const selected = workflows.value?.[key];
  if (selected)
  {
    selectedKey.value = key;
    emit('workflow-selected', {id: key, item: selected});
  }
}

onMounted(() =>
{
  workflowsAsync.execute();
});
</script>

<style scoped>
.error-state {
  padding: 20px;
}
</style>