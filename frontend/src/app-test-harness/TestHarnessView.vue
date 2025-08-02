<template>
  <div class="test-harness-view">
    <div class="left-panel">
      <n-tabs type="line" animated v-model:value="activeTab">
        <n-tab-pane name="workflow" tab="工作流">
          <workflow-selector @workflow-selected="handleSelection" />
        </n-tab-pane>
        <n-tab-pane name="tuum" tab="祝祷">
          <tuum-selector @tuum-selected="handleSelection" />
        </n-tab-pane>
      </n-tabs>
    </div>
    <div class="right-panel">
      <execution-interface
          v-if="selectedItem && selectedItem.item.isSuccess"
          :key="selectedItem.id"
          :config="selectedItem.item.data"
          :config-type="selectedItem.type"
      />
      <n-empty v-else description="请先从左侧选择一个工作流或祝祷进行测试" class="empty-state" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import WorkflowSelector from './components/WorkflowSelector.vue';
import TuumSelector from './components/TuumSelector.vue';
import ExecutionInterface from './components/ExecutionInterface.vue';
import type { WorkflowResourceItem, TuumResourceItem } from '@/app-workbench/stores/workbenchStore';
import { NEmpty, NTabs, NTabPane } from 'naive-ui';

type SelectedItem = {
  id: string;
  type: 'workflow' | 'tuum';
  item: WorkflowResourceItem | TuumResourceItem;
}

const activeTab = ref<'workflow' | 'tuum'>('workflow');
const selectedItem = ref<SelectedItem | null>(null);

function handleSelection(payload: { id: string, item: WorkflowResourceItem | TuumResourceItem }) {
  selectedItem.value = {
    ...payload,
    type: activeTab.value,
  };
}
</script>

<style scoped>
.test-harness-view {
  display: flex;
  height: 100%;
  width: 100%;
}

.left-panel {
  width: 350px;
  border-right: 1px solid #e8e8e8;
  padding: 0 16px;
  overflow-y: auto;
}

.right-panel {
  flex-grow: 1;
  display: flex;
  flex-direction: column;
}

.empty-state {
  margin: auto;
}
</style>