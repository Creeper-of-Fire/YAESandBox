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
      <!-- 根据不同状态显示不同的 Tooltip 内容 -->
      <n-flex v-if="selectedWorkflowConfig" :size="4" vertical>
        <n-space align="center">
          <n-text>当前使用:</n-text>
          <n-tag round size="small" type="primary">{{ selectedWorkflowConfig.name }}</n-tag>
        </n-space>
        <n-text :depth="3" style="font-size: 12px;">右键可重新选择或清除。</n-text>
      </n-flex>
      <n-flex v-else-if="props.expectedInputs && props.expectedInputs.length > 0" :size="4" vertical>
        <n-text>点击选择生成器，此场景需要输入:</n-text>
        <n-space>
          <n-tag v-for="input in props.expectedInputs" :key="input" round size="small" type="success">
            {{ input }}
          </n-tag>
        </n-space>
        <n-text :depth="3" style="font-size: 12px;">右键可进行操作。</n-text>
      </n-flex>
      <n-flex v-else :size="4" vertical>
        <n-text>点击选择 AI 生成器。</n-text>
        <n-text :depth="3" style="font-size: 12px;">右键可进行操作。</n-text>
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
      <n-alert v-if="props.expectedInputs && props.expectedInputs.length > 0" title="当前需求" type="info" style="margin-bottom: 16px;">
        此场景需要一个能接收以下输入的工作流:
        <n-space :style="{ marginTop: '8px' }">
          <n-tag v-for="input in props.expectedInputs" :key="input" type="success">
            {{ input }}
          </n-tag>
        </n-space>
      </n-alert>
      <n-list bordered clickable hoverable>
        <n-list-item v-for="workflow in sortedAndEnhancedWorkflows" :key="workflow.id"
                     @click="selectWorkflow(workflow.id)">
          <n-thing v-if="workflow.resource" :title="workflow.resource.name">
            <template #description>
              <n-space>
                <n-tag v-for="tag in workflow.tags" :key="tag.label" :type="tag.type" round size="small">
                  {{ tag.label }}
                </n-tag>
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
import {useWorkflowSelector} from '@yaesandbox-frontend/core-services/composables';
import type {WorkflowConfig} from '@yaesandbox-frontend/core-services/types';

// 定义 Props & Emits
const props = defineProps<{
  storageKey: string;
  expectedInputs?: string[];
}>();

const emit = defineEmits<{
  // 当按钮被点击且已配置工作流时触发
  (e: 'click', config: WorkflowConfig): void
}>();

// --- 核心逻辑 ---

const sortedAndEnhancedWorkflows = computed(() =>
{
  if (!availableWorkflows.value) return [];
  const expected = new Set(props.expectedInputs || []);

  const enhanced = availableWorkflows.value.map(workflow =>
  {
    const wfInputs = new Set(workflow.resource?.workflowInputs || []);
    const matchedInputs = new Set([...expected].filter(x => wfInputs.has(x)));
    const extraInputs = new Set([...wfInputs].filter(x => !expected.has(x)));

    // 评分: 1. 优先满足缺失少的, 2. 其次是冗余少的
    const missingCount = expected.size - matchedInputs.size;
    const extraCount = extraInputs.size;

    // 生成标签并排序（匹配的在前）
    const tags = [
      ...Array.from(matchedInputs).map(label => ({label, type: 'success' as const})),
      ...Array.from(extraInputs).map(label => ({label, type: 'default' as const}))
    ];

    return {
      ...workflow,
      score: {missingCount, extraCount},
      tags,
    };
  });

  // 排序
  return enhanced.sort((a, b) =>
  {
    // 优先按缺失数量升序排
    if (a.score.missingCount !== b.score.missingCount)
    {
      return a.score.missingCount - b.score.missingCount;
    }
    // 缺失数量相同，按冗余数量升序排
    return a.score.extraCount - b.score.extraCount;
  });
});


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
  } else
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
  } else if (key === 'clear')
  {
    clearSelection();
  }
}
</script>