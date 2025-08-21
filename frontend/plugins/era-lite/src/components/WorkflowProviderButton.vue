<template>
  <div>
    <!-- 主按钮，是用户交互的核心 -->
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
      <n-list bordered clickable hoverable>
        <n-list-item v-for="workflow in availableWorkflows" :key="workflow.id" @click="selectWorkflow(workflow.id)">
          <n-thing v-if="workflow.resource"
                   :description="`Inputs: ${workflow.resource.workflowInputs.join(', ') || 'None'}`"
                   :title="workflow.resource.name"/>
        </n-list-item>
      </n-list>
    </n-modal>
  </div>
</template>

<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NButton, NDropdown, NList, NListItem, NModal, NSpin, NThing} from 'naive-ui';
import {useWorkflowSelector} from '@yaesandbox-frontend/core-services/composables';
import type {WorkflowConfig} from '@yaesandbox-frontend/core-services/types';

// 定义 Props & Emits
const props = defineProps<{
  storageKey: string;
}>();

const emit = defineEmits<{
  // 当按钮被点击且已配置工作流时触发
  (e: 'click', config: WorkflowConfig): void
}>();

// --- 核心逻辑 ---

// 1. 复用 useWorkflowSelector 来处理状态和持久化
const {
  isModalVisible,
  selectedWorkflowConfig,
  availableWorkflows,
  isProviderLoading,
  openSelectorModal,
  selectWorkflow,
  clearSelection,
} = useWorkflowSelector(props.storageKey);

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