<!-- src/app-workbench/components/.../GlobalResourcePanel.vue -->
<template>
  <HeaderAndBodyLayout>
    <template #header>
      <n-flex justify="space-between">
        <n-h4>
          全局资源
        </n-h4>
        <InlineInputPopover
            :content-type="activeTab === 'module' ? 'select-and-input' : 'input'"
            :default-name-generator="moduleDefaultNameGenerator"
            :initial-value="`新建${currentTabLabel}`"
            :input-placeholder="`请输入新的${currentTabLabel}名称`"
            :select-options="moduleTypeOptions"
            :select-placeholder="'请选择模块类型'"
            :title="`新建全局${currentTabLabel}`"
            @confirm="handleCreateNew"
        >
          <n-tooltip trigger="hover">
            <template #trigger>
              <n-button
                  tag="h4"
                  text
                  type="primary"
              >
                新建{{ currentTabLabel }}
              </n-button>
            </template>
            新建全局{{ currentTabLabel }}
          </n-tooltip>
        </InlineInputPopover>
      </n-flex>

      <!-- 状态一：正在加载 -->
      <div v-if="aggregatedIsLoading" class="panel-state-wrapper">
        <n-spin size="small"/>
        <span style="margin-left: 8px;">正在加载...</span>
      </div>

      <!-- 状态二：加载出错 -->
      <div v-else-if="aggregatedError" class="panel-state-wrapper">
        <n-alert :show-icon="true" title="加载错误" type="error">
          无法加载全局资源。
        </n-alert>
        <!-- 将重试按钮放在 alert 下方，作为独立的错误恢复操作 -->
        <n-button block secondary strong style="margin-top: 12px;" @click="executeAll">
          重试
        </n-button>
      </div>

      <!-- 状态三：加载成功，显示数据 -->
      <n-tabs
          v-model:value="activeTab"
          :animated="false"
          class="global-resource-tabs"
          justify-content="space-evenly"
          type="segment"
      >
        <!-- 工作流标签页 -->
        <n-tab name="workflow" tab="工作流"/>
        <!-- 步骤标签页 -->
        <n-tab name="step" tab="步骤"/>
        <!-- 模块标签页 -->
        <n-tab name="module" tab="模块"/>
      </n-tabs>


    </template>

    <template #body>
      <div v-if="activeTab===`workflow`">
        <draggable
            v-if="workflowsList.length > 0"
            v-model="workflowsList"
            :animation="150"
            :clone="handleResourceClone"
            :group="{ name: 'workflows-group', pull: 'clone', put: false }"
            :setData="handleSetData"
            :sort="false"
            class="resource-list"
            item-key="id"
        >
          <div v-for="element in workflowsList"
               :key="element.id"
               :data-drag-id="element.id"
               data-drag-type="workflow"
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
        <n-empty v-else class="empty-container" description="无全局工作流" small/>
      </div>
      <div v-if="activeTab===`step`">
        <draggable
            v-if="stepsList.length > 0"
            v-model="stepsList"
            :animation="150"
            :clone="handleResourceClone"
            :group="{ name: 'steps-group', pull: 'clone', put: false }"
            :setData="handleSetData"
            :sort="false"
            class="resource-list"
            item-key="id"
        >
          <div v-for="element in stepsList"
               :key="element.id"
               :data-drag-id="element.id"
               data-drag-type="step"
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
        <n-empty v-else class="empty-container" description="无全局步骤" small/>
      </div>
      <div v-if="activeTab===`module`">
        <draggable
            v-if="modulesList.length > 0"
            v-model="modulesList"
            :animation="150"
            :clone="handleResourceClone"
            :group="{ name: 'modules-group', pull: 'clone', put: false }"
            :setData="handleSetData"
            :sort="false"
            class="resource-list"
            item-key="id"
        >
          <div v-for="element in modulesList"
               :key="element.id"
               :data-drag-id="element.id"
               data-drag-type="module"
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
        <n-empty v-else class="empty-container" description="无全局模块" small/>
      </div>
    </template>
  </HeaderAndBodyLayout>
</template>


<script lang="ts" setup>
import {computed, h, onMounted, ref} from 'vue';
import {NAlert, NButton, NEmpty, NFlex, NH4, NSpin, NTab, NTabs, useDialog, useMessage} from 'naive-ui';
import {deepCloneWithNewIds, useWorkbenchStore} from '@/app-workbench/stores/workbenchStore';
import type {ConfigObject, ConfigType} from "@/app-workbench/services/EditSession";
import {VueDraggable as draggable} from "vue-draggable-plus";
import type {GlobalResourceItem} from "@/types/ui.ts";
import GlobalResourceListItem from './GlobalResourceListItem.vue';
import HeaderAndBodyLayout from "@/app-workbench/layouts/HeaderAndBodyLayout.vue";
import {createBlankConfig} from "@/app-workbench/utils/createBlankConfig.ts";
import InlineInputPopover from "@/app-workbench/components/share/InlineInputPopover.vue";

// 定义我们转换后给 draggable 用的数组项的类型
type DraggableResourceItem<T> = {
  id: string; // 原始 Record 的 key
  item: GlobalResourceItem<T>; // 原始 Record 的 value
};

const activeTab = ref<'workflow' | 'step' | 'module'>('step'); // 默认激活“步骤”标签页

const emit = defineEmits<{ (e: 'start-editing', payload: { type: ConfigType; id: string }): void; }>();
const workbenchStore = useWorkbenchStore();
const dialog = useDialog();
const message = useMessage();

const workflowsAsync = workbenchStore.globalWorkflowsAsync;
const stepsAsync = workbenchStore.globalStepsAsync;
const modulesAsync = workbenchStore.globalModulesAsync;
const workflows = computed(() => workflowsAsync.state);
const steps = computed(() => stepsAsync.state);
const modules = computed(() => modulesAsync.state);

// 为所有资源类型创建可用于 v-model 的列表
const workflowsList = computed({
  get: () => workflows.value ? Object.entries(workflows.value).map(([id, item]) => ({id, item})) : [],
  set: () =>
  {
  }
});
const stepsList = computed({
  get: () => steps.value ? Object.entries(steps.value).map(([id, item]) => ({id, item})) : [],
  set: () =>
  {
  }
});
const modulesList = computed({
  get: () => modules.value ? Object.entries(modules.value).map(([id, item]) => ({id, item})) : [],
  set: () =>
  {
  }
});

const aggregatedIsLoading = computed(() => workflowsAsync.isLoading || stepsAsync.isLoading || modulesAsync.isLoading);
const aggregatedError = computed(() => workflowsAsync.error || stepsAsync.error || modulesAsync.error);

function executeAll()
{
  workflowsAsync.execute();
  stepsAsync.execute();
  modulesAsync.execute();
}


// 组件挂载时触发数据加载
onMounted(() =>
{
  executeAll()
});

/**
 * 显示资源加载错误的详细信息。
 * @param {string} errorMessage - 错误信息。
 * @param {string | null | undefined} originJsonString - 原始的 JSON 字符串（如果可用）。
 */
function showErrorDetail(errorMessage: string, originJsonString: string | null | undefined)
{
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
 * 计算属性，用于在 Tooltip 中显示当前激活的标签页名称
 */
const currentTabLabel = computed(() =>
{
  switch (activeTab.value)
  {
    case 'workflow':
      return '工作流';
    case 'step':
      return '步骤';
    case 'module':
      return '模块';
    default:
      return '资源';
  }
});

const moduleTypeOptions = computed(() =>
{
  const schemas = workbenchStore.moduleSchemasAsync.state;
  if (!schemas) return [];
  return Object.keys(schemas).map(key =>
  {
    const metadata = workbenchStore.moduleMetadata[key];
    return {
      label: metadata?.classLabel || schemas[key].title || key,
      value: key,
    };
  });
});

const moduleDefaultNameGenerator = (newType: string, options: any[]) =>
{
  if (newType)
  {
    const schema = workbenchStore.moduleSchemasAsync.state[newType];
    const defaultName = schema?.properties?.name?.default;
    if (typeof defaultName === 'string')
    {
      return defaultName;
    }
    return options.find(opt => opt.value === newType)?.label || '新模块';
  }
  return '';
};

/**
 * 处理 InlineInputPopover 确认事件
 */
async function handleCreateNew(payload: { name?: string, type?: string })
{
  const name = payload.name;
  if (!name)
  {
    message.error('请输入有效的名称');
    return;
  }
  const resourceType = activeTab.value;
  const moduleType = payload.type;

  if (resourceType === 'module' && !moduleType)
  {
    message.error('创建模块时必须选择模块类型');
    return;
  }

  try
  {
    const blankConfig =
        resourceType === 'module'
            ? createBlankConfig('module', name, {moduleType: moduleType!})
            : resourceType === 'workflow'
                ? createBlankConfig('workflow', name)
                : createBlankConfig('step', name);

    await workbenchStore.createGlobalConfig(blankConfig);
    message.success(`成功创建全局${currentTabLabel.value}“${name}”！`);
  } catch (e)
  {
    message.error(`创建失败: ${(e as Error).message}`);
  }
}


/**
 * vue-draggable-plus 的 :clone prop 处理函数。
 * 在此函数中，我们将原始资源项中的 'data' 部分深拷贝，并递归刷新所有 configId。
 * 注意，从本页面拖拽到编辑中的draggable包裹的列表时，会走:clone prop调用deepCloneWithNewIds，于是configId会自动刷新。
 * 而从本页面到acquireEditSession走的是HTML5的原生路径，不会触发configId的刷新。
 * （如果这引入了什么问题，也可以重新在acquireEditSession中进行修改，目前来看这样最好。）
 * @param {DraggableResourceItem<ConfigObject>} originalResourceItem - 原始的资源列表项。
 * @returns {ConfigObject | null} - 克隆并刷新 ID 后的纯数据对象，作为拖拽的数据负载。
 */
function handleResourceClone(originalResourceItem: DraggableResourceItem<ConfigObject>): ConfigObject | null
{
  if (originalResourceItem.item.isSuccess)
  {
    // 使用 deepCloneWithNewIds 处理原始数据
    return deepCloneWithNewIds(originalResourceItem.item.data);
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
function startEditing(payload: { type: ConfigType; id: string })
{
  emit('start-editing', payload);
}

/**
 * setData 回调函数。
 * 这是 vue-draggable-plus 官方提供的与原生 dataTransfer 交互的钩子。
 * 当拖拽开始时，库会调用这个函数。
 * @param dataTransfer - 原生的 DataTransfer 对象
 * @param dragEl - 被拖拽的 DOM 元素 (即我们 v-for 的那个 div)
 */
function handleSetData(dataTransfer: DataTransfer, dragEl: HTMLElement)
{
  // 从被拖拽的元素上读取我们之前设置好的 data-* 属性
  const type = dragEl.dataset.dragType as ConfigType | undefined;
  const id = dragEl.dataset.dragId;

  if (type && id)
  {
    // 将数据打包成JSON，安全地存入 dataTransfer
    const data = JSON.stringify({type, id});
    dataTransfer.setData('text/plain', data);

    // 设置一个自定义类型，用于在 dragenter 事件中进行类型检查。
    // 这个类型本身不携带数据，它的存在就是一个“标记”。
    const customDragType = `application/vnd.workbench.item.${type}`;
    dataTransfer.setData(customDragType, id); // 值可以是任意非空字符串，比如 id
  }

  dataTransfer.effectAllowed = 'copy'
}

</script>

<style scoped>
.panel-state-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  min-height: 200px;
  /* 确保状态容器也能滚动 */
  overflow: auto;
}

/* 确保空状态也能正确显示 */
.empty-container {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
}

.resource-list {
  display: flex;
  flex-direction: column;
  gap: 8px; /* 列表项之间的间距 */
  padding-top: 8px; /* 列表顶部留出一些空间 */
}

</style>
<!-- END OF FILE -->