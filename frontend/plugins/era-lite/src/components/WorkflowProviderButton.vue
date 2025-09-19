<template>
  <div>
    <n-popover trigger="hover">
      <template #trigger>
        <!-- 主按钮 -->
        <n-button
            :disabled="isProviderLoading"
            :ghost="!!selectedWorkflowConfig"
            :type="selectedWorkflowConfig ? 'primary' : 'default'"
            block
            size="large"
            style="user-select: none;"
            @click="handleClick"
            @contextmenu.prevent="handleContextMenu"
        >
          <template v-if="isProviderLoading" #icon>
            <n-spin :size="20"/>
          </template>
          <span v-if="isProviderLoading">加载中...</span>
          <span v-else>{{ buttonText }}</span>
        </n-button>
      </template>
      <n-flex :size="8" style="max-width: 300px;" vertical>

        <!-- 状态一：已选择一个工作流 -->
        <template v-if="selectedWorkflowConfig">
          <n-text strong>当前已配置</n-text>
          <n-thing>
            <template #header>
              <n-text type="info">{{ selectedWorkflowConfig.name }}</n-text>
            </template>
            <template #description>
              <n-space size="small">
                <n-tag v-for="tag in selectedWorkflowConfig.tags" :key="tag" round size="small">{{ tag }}</n-tag>
              </n-space>
            </template>
          </n-thing>
          <n-divider style="margin: 4px 0;"/>
          <n-text :depth="3" style="font-size: 12px;">
            左键点击以执行，右键点击可重新选择或清除。
          </n-text>
        </template>

        <!-- 状态二：未选择，但有明确的输入需求 -->
        <template v-else-if="filter.expectedInputs?.length">
          <n-text strong>需要一个生成器</n-text>
          <n-text>此场景需要一个能处理以下输入的AI工作流：</n-text>
          <n-space>
            <n-tag v-for="input in filter.expectedInputs" :key="input" round type="success">{{ input }}</n-tag>
          </n-space>
          <n-divider style="margin: 4px 0;"/>
          <n-text :depth="3" style="font-size: 12px;">
            点击按钮以从列表中选择一个匹配的工作流。
          </n-text>
        </template>

        <!-- 状态三：未选择，且没有特定需求 -->
        <template v-else>
          <n-text strong>未配置</n-text>
          <n-text>点击以选择一个通用的AI工作流来执行任务。</n-text>
          <n-divider style="margin: 4px 0;"/>
          <n-text :depth="3" style="font-size: 12px;">
            右键点击可进行更多操作。
          </n-text>
        </template>

      </n-flex>
    </n-popover>

    <!-- 右键菜单，用于重新配置 -->
    <n-dropdown
        :options="dropdownOptions"
        :show="showDropdown"
        :x="x"
        :y="y"
        placement="bottom-start"
        trigger="manual"
        @clickoutside="showDropdown = false"
        @select="handleDropdownSelect"
    />

    <!-- 工作流选择模态框 -->
    <n-modal
        v-model:show="isModalVisible"
        preset="card"
        style="width: 600px"
        title="选择一个工作流"
    >
      <n-alert v-if="filter.expectedInputs?.length" style="margin-bottom: 1rem;" title="场景需求" type="info">
        此任务期望生成器能处理以下输入：
        <n-space :style="{ marginTop: '8px' }">
          <n-tag v-for="input in filter.expectedInputs" :key="input" type="success">{{ input }}</n-tag>
        </n-space>
      </n-alert>
      <n-list bordered clickable hoverable>
        <n-list-item v-for="workflow in filteredAndSortedWorkflows" :key="workflow.id"
                     @click="selectWorkflow(workflow.id)">
          <n-thing v-if="workflow.resource" :title="workflow.resource.name">
            <template #description>
              <n-space align="center" size="small">
                <!-- 智能提示标签 -->
                <n-tag v-if="workflow.score.missingInputs === 0 && workflow.score.extraInputs === 0" round size="small" type="success">
                  完美匹配
                </n-tag>
                <n-tag v-if="workflow.score.missingInputs > 0" round size="small" type="warning">缺少 {{ workflow.score.missingInputs }}
                  项输入
                </n-tag>

                <!-- 工作流自身标签 -->
                <n-tag v-for="tag in workflow.resource.tags" :key="tag" round size="small">{{ tag }}</n-tag>
              </n-space>
            </template>
          </n-thing>
        </n-list-item>
      </n-list>
    </n-modal>
  </div>
</template>

<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NButton, NDropdown, NList, NListItem, NModal, NSpace, NSpin, NTag, NThing} from 'naive-ui';
import {useFilteredWorkflowSelector, type WorkflowFilter} from '@yaesandbox-frontend/core-services/composables';
import type {WorkflowConfig} from '@yaesandbox-frontend/core-services/types';

// 定义 Props & Emits
const props = defineProps<{
  storageKey: string;
  filter: WorkflowFilter;
}>();

const emit = defineEmits<{
  // 当按钮被点击且已配置工作流时触发
  (e: 'click', config: WorkflowConfig): void
}>();

// --- 核心逻辑 ---
const filterRef = ref(props.filter);

// 1. 复用 useWorkflowSelector 来处理状态和持久化
const {
  isModalVisible,
  selectedWorkflowConfig,
  isProviderLoading,
  openSelectorModal,
  selectWorkflow,
  clearSelection,
  filteredAndSortedWorkflows,
} = useFilteredWorkflowSelector(props.storageKey, filterRef);

defineExpose({
  selectedWorkflowConfig: selectedWorkflowConfig
})

// 2. 主按钮点击逻辑
function handleClick()
{
  if (selectedWorkflowConfig.value)
  {
    // 如果已经配置，就向外发射事件，并携带配置信息
    emit('click', selectedWorkflowConfig.value);
  }
  else
  {
    // 如果未配置，就打开选择模态框，引导用户配置
    openSelectorModal();
  }
}

// 3. UI 状态计算
const buttonText = computed(() =>
{
  return selectedWorkflowConfig.value?.name || '配置AI生成器';
});

// --- 右键菜单逻辑 (与之前的 WorkflowSelector 组件相同) ---
const showDropdown = ref(false);
const x = ref(0);
const y = ref(0);

const dropdownOptions = computed(() => [
  {
    label: '重新选择工作流',
    key: 'select',
  },
  {
    label: '清除选择',
    key: 'clear',
    disabled: !selectedWorkflowConfig.value,
  },
]);

function handleContextMenu(e: MouseEvent)
{
  showDropdown.value = true;
  x.value = e.clientX;
  y.value = e.clientY;
}

function handleDropdownSelect(key: string | number)
{
  showDropdown.value = false;
  if (key === 'select')
  {
    openSelectorModal();
  }
  else if (key === 'clear')
  {
    clearSelection();
  }
}
</script>