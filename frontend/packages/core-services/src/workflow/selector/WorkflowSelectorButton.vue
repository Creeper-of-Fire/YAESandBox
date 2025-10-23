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
  </div>
</template>

<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NButton, NDropdown, NPopover, NSpace, NSpin, NTag, NThing} from 'naive-ui';
import {type MatchingOptions, type WorkflowFilter} from '../../composables.ts';
import type {WorkflowConfig} from "../../types";
import {useFilteredWorkflowSelectorModal} from "./useFilteredWorkflowSelectorModal.tsx";

// 定义 Props & Emits
const props = defineProps<{
  storageKey: string;
  filter: WorkflowFilter;
  initialOptions?: Partial<MatchingOptions>;
}>();

const emit = defineEmits<{
  // 当按钮被点击且已配置工作流时触发
  (e: 'click', config: WorkflowConfig): void
}>();

// --- 核心逻辑 ---
const filterRef = ref(props.filter);

// 复用 useWorkflowSelector 来处理状态和持久化
const {
  selectedWorkflowConfig,
  isProviderLoading,
  clearSelection,
  openSelectorModal,
} = useFilteredWorkflowSelectorModal(props.storageKey, filterRef, props.initialOptions);

defineExpose({
  selectedWorkflowConfig: selectedWorkflowConfig
})

// 主按钮点击逻辑
async function handleClick()
{
  if (selectedWorkflowConfig.value)
  {
    // 如果已经配置，就向外发射事件，并携带配置信息
    emit('click', selectedWorkflowConfig.value);
  }
  else
  {
    await openSelectorModal();
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