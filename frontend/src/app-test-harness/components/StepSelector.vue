<template>
  <div>
    <n-h4>选择步骤</n-h4>
    <n-spin :show="stepsAsync.isLoading">
      <div v-if="stepsAsync.error" class="error-state">
        <n-alert title="加载失败" type="error">
          无法加载步骤列表。
        </n-alert>
        <n-button block secondary strong style="margin-top: 12px;" @click="stepsAsync.execute(0)">重试</n-button>
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
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore';
import type {MenuOption} from 'naive-ui';
import {NAlert, NButton, NH4, NIcon, NMenu, NSpin} from 'naive-ui';
import {StepIcon} from '@/utils/icons';
import type {StepProcessorConfig} from "@/app-workbench/types/generated/workflow-config-api-client";

const workbenchStore = useWorkbenchStore();
const stepsAsync = workbenchStore.globalStepsAsync;
const steps = computed(() => stepsAsync.state);

const emit = defineEmits(['step-selected']);

const selectedKey = ref<string | null>(null);

type SuccessStepResourceItem = { isSuccess: true; data: StepProcessorConfig };

const menuOptions = computed<MenuOption[]>(() =>
{
  if (!steps.value) return [];
  return Object.entries(steps.value)
      .filter(
          (entry): entry is [string, SuccessStepResourceItem] => entry[1]?.isSuccess === true
      )
      .map(([id, item]) => ({
        label: item.data.name,
        key: id,
        icon: () => h(NIcon, {component: StepIcon})
      }));
});

function handleSelect(key: string)
{
  const selected = steps.value?.[key];
  if (selected)
  {
    selectedKey.value = key;
    emit('step-selected', {id: key, item: selected});
  }
}

onMounted(() =>
{
  if (!stepsAsync.isReady)
  {
    stepsAsync.execute();
  }
});
</script>

<style scoped>
.error-state {
  padding: 20px;
}
</style>