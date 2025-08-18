<template>
  <div>
    <n-h4>选择枢机</n-h4>
    <n-spin :show="tuumsAsync.isLoading">
      <div v-if="tuumsAsync.error" class="error-state">
        <n-alert title="加载失败" type="error">
          无法加载枢机列表。
        </n-alert>
        <n-button block secondary strong style="margin-top: 12px;" @click="tuumsAsync.execute(0)">重试</n-button>
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
import {computed, h, onMounted, ref, watch} from 'vue';
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore';
import type {MenuOption} from 'naive-ui';
import {NAlert, NButton, NH4, NIcon, NMenu, NSpin} from 'naive-ui';
import {TuumIcon} from '@/utils/icons';
import type {TuumConfig} from "@/app-workbench/types/generated/workflow-config-api-client";

const workbenchStore = useWorkbenchStore();
const tuumsAsync = workbenchStore.globalTuumsAsync;
const tuums = computed(() => tuumsAsync.state);

const emit = defineEmits(['tuum-selected']);

const selectedKey = ref<string | null>(null);

type SuccessTuumResourceItem = { isSuccess: true; data: TuumConfig };

const menuOptions = computed<MenuOption[]>(() =>
{
  if (!tuums.value) return [];
  return Object.entries(tuums.value)
      .filter(
          (entry): entry is [string, SuccessTuumResourceItem] => entry[1]?.isSuccess === true
      )
      .map(([id, item]) => ({
        label: item.data.name,
        key: id,
        icon: () => h(NIcon, {component: TuumIcon})
      }));
});

function handleSelect(key: string)
{
  const selected = tuums.value?.[key];
  if (selected)
  {
    selectedKey.value = key;
    emit('tuum-selected', {id: key, item: selected});
  }
}

onMounted(() =>
{
  if (!tuumsAsync.isReady)
  {
    tuumsAsync.execute();
  }
});
</script>

<style scoped>
.error-state {
  padding: 20px;
}
</style>