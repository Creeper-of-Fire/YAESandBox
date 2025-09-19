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
      <!-- 场景需求提示 -->
      <n-alert v-if="filter.expectedInputs?.length || filter.requiredTags?.length" style="margin-bottom: 1rem;" title="场景需求" type="info">
        <n-space vertical>
          <div v-if="filter.expectedInputs?.length">
            此任务期望生成器能处理以下输入：
            <n-space :style="{ marginTop: '8px' }">
              <n-tag round v-for="input in filter.expectedInputs" :key="input" type="success">{{ input }}</n-tag>
            </n-space>
          </div>
          <div v-if="filter.requiredTags?.length">
            并期望生成器包含以下标签：
            <n-space :style="{ marginTop: '8px' }">
              <n-tag v-for="tag in filter.requiredTags" :key="tag" type="info">{{ tag }}</n-tag>
            </n-space>
          </div>
        </n-space>
      </n-alert>

      <!-- 配置区域 -->
      <n-card size="small" style="margin-bottom: 1rem;">
        <n-flex justify="space-around">
          <!-- 输入匹配模式配置 -->
          <n-flex :size="8" vertical>
            <n-text strong>输入参数匹配</n-text>
            <n-radio-group v-model:value="inputMatchingMode" name="input-mode">
              <n-popover trigger="hover">
                <template #trigger>
                  <n-radio value="strict">严格</n-radio>
                </template>
                必须完美匹配所有输入，不多不少。
              </n-popover>
              <n-popover trigger="hover">
                <template #trigger>
                  <n-radio value="normal">普通</n-radio>
                </template>
                可以接受场景提供的额外输入（被忽略）。
              </n-popover>
              <n-popover trigger="hover">
                <template #trigger>
                  <n-radio value="relaxed">宽松</n-radio>
                </template>
                允许工作流缺少部分输入。
              </n-popover>
            </n-radio-group>
          </n-flex>

          <n-divider vertical/>

          <!-- 标签匹配模式配置 -->
          <n-flex :size="8" vertical>
            <n-text strong>标签匹配</n-text>
            <n-radio-group v-model:value="tagMatchingMode" name="tag-mode">
              <n-popover trigger="hover">
                <template #trigger>
                  <n-radio value="need">必须</n-radio>
                </template>
                工作流必须包含所有场景需求的标签。
              </n-popover>
              <n-popover trigger="hover">
                <template #trigger>
                  <n-radio value="prefer">偏好</n-radio>
                </template>
                仅将标签作为排序依据，不强制要求。
              </n-popover>
            </n-radio-group>
          </n-flex>
        </n-flex>
      </n-card>

      <n-list bordered clickable hoverable>
        <n-list-item v-for="workflow in filteredAndSortedWorkflows" :key="workflow.id"
                     @click="selectWorkflow(workflow.id)">
          <n-thing :title="workflow.resource.name">
            <template #description>
              <n-space align="center" size="small" wrap>
                <!-- 渲染输入参数 -->
                <n-tag v-for="(status, name) in workflow.matchDetails.inputs" :key="name" :type="getInputTagType(status)" round
                       size="small">
                  {{ name }}
                </n-tag>
                <!-- 渲染标签 -->
                <n-tag v-for="(status, name) in workflow.matchDetails.tags" :key="name" :type="getTagTagType(status)"
                        size="small">
                  {{ name }}
                </n-tag>
              </n-space>
            </template>
          </n-thing>
        </n-list-item>
        <n-empty v-if="!filteredAndSortedWorkflows.length" description="在当前模式下没有找到匹配的工作流" style="padding: 2rem;"/>
      </n-list>
    </n-modal>
  </div>
</template>

<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NButton, NDropdown, NList, NListItem, NModal, NPopover, NSpace, NSpin, NTag, NThing} from 'naive-ui';
import {
  type InputMatchingMode,
  type MatchingOptions,
  type TagMatchingMode,
  useFilteredWorkflowSelector,
  useScopedStorage,
  type WorkflowFilter
} from '../../composables.ts';
import type {WorkflowConfig} from "../../types";

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

const inputMatchingMode = useScopedStorage<InputMatchingMode>(`${props.storageKey}-input-matching-mode`, props.initialOptions?.inputs || 'normal');
const tagMatchingMode = useScopedStorage<TagMatchingMode>(`${props.storageKey}-tag-mathing-mode`, props.initialOptions?.tags || 'prefer');

const matchingOptions = computed<MatchingOptions>(() => ({
  inputs: inputMatchingMode.value,
  tags: tagMatchingMode.value,
}));

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
} = useFilteredWorkflowSelector(props.storageKey, filterRef, matchingOptions);

function getInputTagType(status: 'matched' | 'unprovided' | 'unconsumed'): 'success' | 'error' | 'default'
{
  switch (status)
  {
    case 'matched':
      return 'success'; // 匹配的输入，绿色
    case 'unprovided':
      return 'error';   // 缺失的输入，红色（致命）
    case 'unconsumed':
      return 'default'; // 未使用的输入，灰色（中性）
  }
}

function getTagTagType(status: 'matched' | 'missing' | 'extra'): 'info' | 'warning' | 'default'
{
  switch (status)
  {
    case 'matched':
      return 'info';    // 匹配的标签，蓝色
    case 'missing':
      return 'warning'; // 缺少的必需标签，橙色
    case 'extra':
      return 'default'; // 工作流自带的额外标签，灰色
  }
}

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