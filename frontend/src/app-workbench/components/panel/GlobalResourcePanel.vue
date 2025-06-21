<!-- src/app-workbench/components/.../GlobalResourcePanel.vue -->
<template>
  <div class="global-resource-panel">
    <n-h4>全局资源</n-h4>
    <n-spin :show="aggregatedIsLoading">
      <!-- 状态一：正在加载 -->
      <div v-if="aggregatedIsLoading" class="panel-state-wrapper">
        <n-spin size="small"/>
        <span style="margin-left: 8px;">正在加载...</span>
      </div>

      <!-- 状态二：加载出错 -->
      <div v-else-if="aggregatedError" class="panel-state-wrapper">
        <n-alert title="加载错误" type="error" :show-icon="true">
          无法加载全局资源。
        </n-alert>
        <!-- 将重试按钮放在 alert 下方，作为独立的错误恢复操作 -->
        <n-button @click="executeAll" block secondary strong style="margin-top: 12px;">
          重试
        </n-button>
      </div>

      <!-- 状态三：加载成功，显示数据 -->
      <div v-else>
        <n-tabs v-model:value="activeTab" type="line" :animated="false" justify-content="space-evenly">

          <!-- 工作流标签页 -->
          <n-tab-pane name="workflows" tab="工作流">
            <div class="scrollable-content">
              <draggable
                  v-if="workflowsList.length > 0"
                  v-model="workflowsList"
                  item-key="id"
                  :group="{ name: 'workflows-group', pull: 'clone', put: false }"
                  :sort="false"
                  :clone="cloneResource"
                  class="resource-list"
                  :setData="handleSetData"
              >
                <!-- 关键: v-for 创建的这个 div 才是被拖拽的单元 (event.item) -->
                <div v-for="element in workflowsList"
                     :key="element.id"
                     data-drag-type="workflow"
                     :data-drag-id="element.id"
                     :data-drag-payload="element.item.isSuccess ? JSON.stringify(element.item.data) : undefined"
                >
                  <GlobalResourceListItem
                      :id="element.id"
                      :item="element.item"
                      type="workflow"
                      @start-editing="startEditing"
                      @show-error-detail="showErrorDetail"
                  />
                </div>
              </draggable>
              <n-empty v-else small description="无全局工作流"/>
            </div>
          </n-tab-pane>

          <!-- 步骤标签页 -->
          <n-tab-pane name="steps" tab="步骤">
            <div class="scrollable-content">
              <draggable
                  v-if="stepsList.length > 0"
                  v-model="stepsList"
                  item-key="id"
                  :group="{ name: 'steps-group', pull: 'clone', put: false }"
                  :sort="false"
                  :clone="cloneResource"
                  class="resource-list"
                  :setData="handleSetData"
              >
                <!-- 关键: data-* 属性必须在 v-for 的这个根 div 上 -->
                <div v-for="element in stepsList"
                     :key="element.id"
                     data-drag-type="step"
                     :data-drag-id="element.id"
                     :data-drag-payload="element.item.isSuccess ? JSON.stringify(element.item.data) : undefined"
                >
                  <GlobalResourceListItem
                      :id="element.id"
                      :item="element.item"
                      type="step"
                      @start-editing="startEditing"
                      @show-error-detail="showErrorDetail"
                  />
                </div>
              </draggable>
              <n-empty v-else small description="无全局步骤"/>
            </div>
          </n-tab-pane>

          <!-- 模块标签页 -->
          <n-tab-pane name="modules" tab="模块">
            <div class="scrollable-content">
              <draggable
                  v-if="modulesList.length > 0"
                  v-model="modulesList"
                  item-key="id"
                  :group="{ name: 'modules-group', pull: 'clone', put: false }"
                  :sort="false"
                  :clone="cloneResource"
                  class="resource-list"
                  :setData="handleSetData"
              >
                <!-- 关键: data-* 属性必须在 v-for 的这个根 div 上 -->
                <div v-for="element in modulesList"
                     :key="element.id"
                     data-drag-type="module"
                     :data-drag-id="element.id"
                     :data-drag-payload="element.item.isSuccess ? JSON.stringify(element.item.data) : undefined"
                >
                  <GlobalResourceListItem
                      :id="element.id"
                      :item="element.item"
                      type="module"
                      @start-editing="startEditing"
                      @show-error-detail="showErrorDetail"
                  />
                </div>
              </draggable>
              <n-empty v-else small description="无全局模块"/>
            </div>
          </n-tab-pane>

        </n-tabs>
      </div>
    </n-spin>
  </div>
</template>

<script setup lang="ts">
import {computed, h, onMounted, ref} from 'vue';
import {NEmpty, NH4, NSpin, NTabPane, NTabs, useDialog} from 'naive-ui';
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore';
import type {ConfigObject, ConfigType} from "@/app-workbench/services/EditSession";
import {VueDraggable as draggable} from "vue-draggable-plus";
import type {GlobalResourceItem} from "@/types/ui.ts";
import GlobalResourceListItem from './GlobalResourceListItem.vue';

// 定义我们转换后给 draggable 用的数组项的类型
type DraggableResourceItem<T> = {
  id: string; // 原始 Record 的 key
  item: GlobalResourceItem<T>; // 原始 Record 的 value
};

const activeTab = ref('steps'); // 默认激活“步骤”标签页

const emit = defineEmits<{ (e: 'start-editing', payload: { type: ConfigType; id: string }): void; }>();
const workbenchStore = useWorkbenchStore();
const dialog = useDialog();

const workflowsAsync = workbenchStore.globalWorkflowsAsync;
const stepsAsync = workbenchStore.globalStepsAsync;
const modulesAsync = workbenchStore.globalModulesAsync;
const workflows = computed(() => workflowsAsync.state);
const steps = computed(() => stepsAsync.state);
const modules = computed(() => modulesAsync.state);

// 为所有资源类型创建可用于 v-model 的列表
const workflowsList = computed({
  get: () => workflows.value ? Object.entries(workflows.value).map(([id, item]) => ({id, item})) : [],
  set: () => {
  }
});
const stepsList = computed({
  get: () => steps.value ? Object.entries(steps.value).map(([id, item]) => ({id, item})) : [],
  set: () => {
  }
});
const modulesList = computed({
  get: () => modules.value ? Object.entries(modules.value).map(([id, item]) => ({id, item})) : [],
  set: () => {
  }
});

const aggregatedIsLoading = computed(() => workflowsAsync.isLoading || stepsAsync.isLoading || modulesAsync.isLoading);
const aggregatedError = computed(() => workflowsAsync.error || stepsAsync.error || modulesAsync.error);

function executeAll() {
  workflowsAsync.execute();
  stepsAsync.execute();
  modulesAsync.execute();
}


// 组件挂载时触发数据加载
onMounted(() => {
  executeAll()
});

/**
 * 显示资源加载错误的详细信息。
 * @param {string} errorMessage - 错误信息。
 * @param {string | null | undefined} originJsonString - 原始的 JSON 字符串（如果可用）。
 */
function showErrorDetail(errorMessage: string, originJsonString: string | null | undefined) {
  const totalMessage = `错误信息: ${errorMessage}\n\n原始JSON: ${originJsonString || '无'}`;
  const messageLines = totalMessage.split('\n');

  dialog.error({
    title: '错误详情',
    content: () => h(
        'div', // 外层容器，可以是一个 div 或者 Fragment
        null,
        messageLines.map(line => h('div', null, line)) // 为每行创建独立的 div
    ),
    positiveText: '确定'
  });
}

/**
 * vuedraggable 的克隆函数 (已修正)。
 * 它接收的是我们新构造的对象 {id, item}。
 * @param {DraggableResourceItem<ConfigObject>} original - 原始的列表项
 * @returns {ConfigObject | null} - 克隆出的纯数据对象，或 null (如果项已损坏)
 */
function cloneResource(original: DraggableResourceItem<ConfigObject>): ConfigObject | null {
  // 我们从 original.item 中判断和获取数据
  if (original.item.isSuccess) {
    // 只克隆数据部分，接收方将处理ID的刷新
    return original.item.data;
  }
  // 不允许拖拽损坏的项
  return null;
}

/**
 * 触发“开始编辑”事件，向上通知父组件打开新的编辑会话。
 * @param {object} payload - 包含类型和ID的对象。
 * @param {ConfigType} payload.type - 配置类型。
 * @param {string} payload.id - 配置ID。
 */
function startEditing(payload: { type: ConfigType; id: string }) {
  emit('start-editing', payload);
}

/**
 * setData 回调函数。
 * 这是 vue-draggable-plus 官方提供的与原生 dataTransfer 交互的钩子。
 * 当拖拽开始时，库会调用这个函数。
 * @param dataTransfer - 原生的 DataTransfer 对象
 * @param dragEl - 被拖拽的 DOM 元素 (即我们 v-for 的那个 div)
 */
function handleSetData(dataTransfer: DataTransfer, dragEl: HTMLElement) {
  // 从被拖拽的元素上读取我们之前设置好的 data-* 属性
  const type = dragEl.dataset.dragType as ConfigType | undefined;
  const id = dragEl.dataset.dragId;

  if (type && id) {
    // 将数据打包成JSON，安全地存入 dataTransfer
    const data = JSON.stringify({ type, id });
    dataTransfer.setData('text/plain', data);
  }

  dataTransfer.effectAllowed = 'copy'
}

</script>

<style scoped>

.global-resource-panel {
  /* 移除内边距，让 n-tabs 的内容区来控制 */
  padding: 0;
  height: 100%;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
}

.global-resource-panel > .n-h4 {
  padding: 0 12px 12px 12px; /* 只给标题添加内边距 */
  flex-shrink: 0;
}

.global-resource-panel > .n-spin {
  flex-grow: 1;
  /* 确保 spin 内容也能撑开 */
  display: flex;
  flex-direction: column;
}

.global-resource-panel .n-spin-content {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.global-resource-panel .n-tabs {
  flex-grow: 1;
  display: flex;
  flex-direction: column;
}

:deep(.n-tabs-pane-wrapper) {
  flex-grow: 1;
  overflow: hidden; /* 直接隐藏，不再需要滚动 */
}

/* 让 tab-pane 成为一个能撑满的容器 */
:deep(.n-tab-pane) {
  height: 100%;
  box-sizing: border-box;
  padding: 0; /* 移除内边距，交由内部的滚动容器控制 */
}

/* 滚动容器样式 */
.scrollable-content {
  height: 100%;
  overflow-y: auto;
  padding: 4px 12px 12px 12px; /* 把内边距放在这里 */
  box-sizing: border-box;
}

.panel-state-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  min-height: 150px;
  text-align: center;
  padding: 0 12px;
}

.resource-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
</style>
<!-- END OF FILE -->