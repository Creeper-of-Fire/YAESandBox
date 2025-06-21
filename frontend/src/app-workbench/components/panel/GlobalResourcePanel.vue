<!-- START OF FILE: src/app-workbench/components/GlobalResourcePanel.vue -->
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
        <n-collapse arrow-placement="right" :default-expanded-names="['steps', 'modules']">
          <!-- ====================================================== -->
          <!--             工作流列表渲染部分 (已修正)                -->
          <!-- ====================================================== -->
          <n-collapse-item title="工作流" name="workflows">
            <div v-if="workflows && Object.keys(workflows).length > 0">
              <!-- 使用 GlobalResourceListItem 渲染工作流项 -->
              <GlobalResourceListItem
                  v-for="(item, id) in workflows"
                  :key="id"
                  :id="id"
                  :item="item"
                  type="workflow"
                  @start-editing="startEditing"
                  @show-error-detail="showErrorDetail"
              />
            </div>
            <n-empty v-else small description="无全局工作流"/>
          </n-collapse-item>

          <!-- ====================================================== -->
            <!--               步骤列表渲染部分 (已修正)                -->
          <!-- ====================================================== -->
          <n-collapse-item title="步骤" name="steps">
            <draggable
                v-if="stepsList.length > 0"
                v-model="stepsList"
                item-key="id"
                :group="{ name: 'steps-group', pull: 'clone', put: false }"
                :sort="false"
                :clone="cloneResource"
            >
              <div v-for="element in stepsList" :key="element.id">
                <!-- 使用 GlobalResourceListItem 渲染步骤项，并传递拖拽数据 -->
                <GlobalResourceListItem
                    :id="element.id"
                    :item="element.item"
                    type="step"
                    :data-drag-payload="element.item.isSuccess ? JSON.stringify(element.item.data) : undefined"
                    @start-editing="startEditing"
                    @show-error-detail="showErrorDetail"
                />
              </div>
            </draggable>
            <n-empty v-else small description="无全局步骤"/>
          </n-collapse-item>

          <!-- ====================================================== -->
          <!--               模块列表渲染部分 (已修正)                -->
          <!-- ====================================================== -->
          <n-collapse-item title="模块" name="modules">
            <draggable
                v-if="modulesList.length > 0"
                v-model="modulesList"
                item-key="id"
                :group="{ name: 'modules-group', pull: 'clone', put: false }"
                :sort="false"
                :clone="cloneResource"
            >
              <div v-for="element in modulesList" :key="element.id">
                <!-- 使用 GlobalResourceListItem 渲染模块项，并传递拖拽数据 -->
                <GlobalResourceListItem
                    :id="element.id"
                    :item="element.item"
                    type="module"
                    :data-drag-payload="element.item.isSuccess ? JSON.stringify(element.item.data) : undefined"
                    @start-editing="startEditing"
                    @show-error-detail="showErrorDetail"
                />
              </div>
            </draggable>
            <n-empty v-else small description="无全局模块"/>
          </n-collapse-item>

        </n-collapse>
      </div>
    </n-spin>
  </div>
</template>

<script setup lang="ts">
import {computed, h, onMounted} from 'vue';
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore.ts';
import {NAlert, NButton, NCollapse, NCollapseItem, NEmpty, NH4, NSpin, useDialog} from 'naive-ui';
import type {ConfigObject, ConfigType} from "@/app-workbench/services/EditSession.ts";
import {VueDraggable as draggable} from "vue-draggable-plus";
import type {GlobalResourceItem} from "@/types/ui.ts";

// 导入新的子组件
import GlobalResourceListItem from './GlobalResourceListItem.vue';

// 定义我们转换后给 draggable 用的数组项的类型
type DraggableResourceItem<T> = {
  id: string; // 原始 Record 的 key
  item: GlobalResourceItem<T>; // 原始 Record 的 value
};

const emit = defineEmits<{
  (e: 'start-editing', payload: { type: ConfigType; id: string }): void;
}>();

const workbenchStore = useWorkbenchStore();
const dialog = useDialog();

// 获取异步数据访问器
const workflowsAsync = workbenchStore.globalWorkflowsAsync;
const stepsAsync = workbenchStore.globalStepsAsync;
const modulesAsync = workbenchStore.globalModulesAsync;
// 解构出完整的异步状态机
const workflows = computed(() => workflowsAsync.state);
const steps = computed(() => stepsAsync.state);
const modules = computed(() => modulesAsync.state);

/**
 * 【新增】创建适用于 vuedraggable 的数组格式数据。
 * 将 Record<string, Item> 转换为 Array<{id: string, item: Item}>
 * 这样既保留了原始ID用于点击事件，又能满足 v-model 的数组要求。
 */
const stepsList = computed({
  get: () => steps.value ? Object.entries(steps.value).map(([id, item]) => ({id, item})) : [],
  set: (newValue) => {
    // 因为是 clone，列表本身不会被修改，但 v-model 要求有 setter
    // 所以这里提供一个空 setter 或者根据需要更新原始数据
  }
});
const modulesList = computed({
  get: () => modules.value ? Object.entries(modules.value).map(([id, item]) => ({id, item})) : [],
  set: (newValue) => {
  }
});

/**
 * 聚合的加载状态。
 * 只要任何一个资源正在加载，整个面板就处于加载状态。
 */
const aggregatedIsLoading = computed(() =>
    workflowsAsync.isLoading ||
    stepsAsync.isLoading ||
    modulesAsync.isLoading
);

/**
 * 聚合的错误状态。
 * 返回第一个遇到的错误对象。
 */
const aggregatedError = computed(() =>
    workflowsAsync.error ||
    stepsAsync.error ||
    modulesAsync.error
);

/**
 * 聚合的执行函数。
 * 调用此函数会触发所有资源的加载（或重试）。
 */
function executeAll() {
  // 依次调用每个 execute 方法。
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
</script>

<style scoped>
.global-resource-panel {
  padding: 12px;
  height: 100%;
  box-sizing: border-box;
  overflow-y: auto;
}

.panel-state-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  min-height: 150px; /* 确保加载/错误状态有足够空间显示 */
  text-align: center;
}

/* GlobalResourceListItem 内部的样式在它自己的文件中定义，这里只需要调整其父容器的布局 */
/* .resource-item 和 .resource-item-damaged 样式已移动到 GlobalResourceListItem.vue */
</style>
<!-- END OF FILE -->