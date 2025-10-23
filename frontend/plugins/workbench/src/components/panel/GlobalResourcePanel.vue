<!-- src/app-workbench/components/.../GlobalResourcePanel.vue -->
<template>
  <div ref="panelRoot" class="global-resource-panel-wrapper">
    <HeaderAndBodyLayout>
      <template #header>
        <n-flex justify="space-between">
          <n-h4>
            全局资源
          </n-h4>
          <n-flex>
            <n-button secondary size="small" @click="importConfig">
              <template #icon>
                <n-icon :component="UploadIcon"/>
              </template>
              导入
            </n-button>
            <n-button ref="createButtonRef" secondary size="small" type="primary" @click="handleCreateNew">
              <template #icon>
                <n-icon :component="AddIcon"/>
              </template>
              新建
            </n-button>
          </n-flex>
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
            size="small"
            type="segment"
        >
          <n-tab name="workflow" tab="工作流"/>
          <n-tab name="tuum" tab="枢机"/>
          <n-tab name="rune" tab="符文"/>
        </n-tabs>

        <n-form-item v-if="activeAvailableTags.length > 0" class="tag-filter-container">
          <n-select
              v-model:value="activeFilterTags"
              :options="activeAvailableTags.map(tag => ({ label: tag, value: tag }))"
              clearable
              filterable
              multiple
              placeholder="按标签筛选..."
              size="small"
          />
        </n-form-item>


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
              item-key="storeId"
              @end="onDragEnd"
              @start="onDragStart"
          >
            <div v-for="element in workflowsList"
                 :key="element.storeId"
                 :data-drag-id="element.storeId"
                 data-drag-type="workflow"
            >
              <GlobalResourceListItem
                  :store-id="element.storeId"
                  type="workflow"
                  @contextmenu="handleContextMenu"
                  @show-error-detail="showErrorDetail"
              />
            </div>
          </draggable>
          <n-empty v-else class="empty-container" description="无全局工作流" small/>
        </div>
        <div v-if="activeTab===`tuum`">
          <draggable
              v-if="tuumsList.length > 0"
              v-model="tuumsList"
              :animation="150"
              :clone="handleResourceClone"
              :group="{ name: 'tuums-group', pull: 'clone', put: false }"
              :setData="handleSetData"
              :sort="false"
              class="resource-list"
              item-key="storeId"
              @end="onDragEnd"
              @start="onDragStart"
          >
            <div v-for="element in tuumsList"
                 :key="element.storeId"
                 :data-drag-id="element.storeId"
                 data-drag-type="tuum"
            >
              <GlobalResourceListItem
                  :store-id="element.storeId"
                  type="tuum"
                  @contextmenu="handleContextMenu"
                  @show-error-detail="showErrorDetail"
              />
            </div>
          </draggable>
          <n-empty v-else class="empty-container" description="无全局枢机" small/>
        </div>
        <div v-if="activeTab===`rune`">
          <draggable
              v-if="runesList.length > 0"
              v-model="runesList"
              :animation="150"
              :clone="handleResourceClone"
              :group="{ name: 'runes-group', pull: 'clone', put: false }"
              :setData="handleSetData"
              :sort="false"
              class="resource-list"
              item-key="storeId"
              @end="onDragEnd"
              @start="onDragStart"
          >
            <div v-for="element in runesList"
                 :key="element.storeId"
                 :data-drag-id="element.storeId"
                 data-drag-type="rune"
            >
              <GlobalResourceListItem
                  :store-id="element.storeId"
                  type="rune"
                  @contextmenu="handleContextMenu"
                  @show-error-detail="showErrorDetail"
              />
            </div>
          </draggable>
          <n-empty v-else class="empty-container" description="无全局符文" small/>
        </div>
      </template>
    </HeaderAndBodyLayout>

    <n-dropdown
        :options="dropdownOptions"
        :show="showDropdown"
        :x="dropdownPosition.x"
        :y="dropdownPosition.y"
        placement="bottom-start"
        trigger="manual"
        @clickoutside="showDropdown = false"
        @select="handleDropdownSelect"
    />
  </div>
</template>


<script lang="tsx" setup>
import {computed, nextTick, reactive, ref} from 'vue';
import {type DropdownOption, NAlert, NButton, NEmpty, NFlex, NH4, NIcon, NSpin, NTab, NTabs, useDialog, useMessage} from 'naive-ui';
import {deepCloneWithNewIds, useWorkbenchStore} from '#/stores/workbenchStore';
import type {AnyConfigObject, ConfigType, GlobalResourceItem} from "@yaesandbox-frontend/core-services/types";
import {VueDraggable as draggable} from "vue-draggable-plus";
import GlobalResourceListItem from './GlobalResourceListItem.vue';
import HeaderAndBodyLayout from "#/layouts/HeaderAndBodyLayout.vue";
import {useGlobalConfigCreationAction} from "#/components/share/itemActions/useConfigItemActions.tsx";
import {AddIcon, EditIcon, TrashIcon, UploadIcon} from "@yaesandbox-frontend/shared-ui/icons";
import {useEditorControlPayload} from "#/services/editor-context/useSelectedConfig.ts";
import {useFilteredGlobalResources} from "#/composables/useFilteredGlobalResources.ts";
import {useRuneTypeSelector} from "#/composables/useRuneTypeSelector.ts";
import {useConfigImportExport} from "#/composables/useConfigImportExport.ts";

// 定义我们转换后给 draggable 用的数组项的类型
type DraggableResourceItem<T> = {
  id: string; // 原始 Record 的 key
  item: GlobalResourceItem<T>; // 原始 Record 的 value
};

const activeTab = ref<'workflow' | 'tuum' | 'rune'>('workflow'); // 默认激活“枢机”标签页

const emit = defineEmits<{ (e: 'start-editing', payload: { type: ConfigType; storeId: string }): void; }>();
const workbenchStore = useWorkbenchStore();
const dialog = useDialog();
const message = useMessage();

const {
  resources: workflows,
  allAvailableTags: workflowTags,
  filterTags: workflowFilterTags,
  isLoading: isWorkflowsLoading,
  error: workflowsError,
  execute: executeWorkflows
} = useFilteredGlobalResources('workflow');
const {
  resources: tuums,
  allAvailableTags: tuumTags,
  filterTags: tuumFilterTags,
  isLoading: isTuumsLoading,
  error: tuumsError,
  execute: executeTuums
} = useFilteredGlobalResources('tuum');
const {
  resources: runes,
  allAvailableTags: runeTags,
  filterTags: runeFilterTags,
  isLoading: isRunesLoading,
  error: runesError,
  execute: executeRunes
} = useFilteredGlobalResources('rune');

// 可写的计算属性，用于 v-model 绑定到 n-select
const activeFilterTags = computed({
  get: () =>
  {
    switch (activeTab.value)
    {
      case 'workflow':
        return workflowFilterTags.value;
      case 'tuum':
        return tuumFilterTags.value;
      case 'rune':
        return runeFilterTags.value;
      default:
        return [];
    }
  },
  set: (tags: string[]) =>
  {
    switch (activeTab.value)
    {
      case 'workflow':
        workflowFilterTags.value = tags;
        break;
      case 'tuum':
        tuumFilterTags.value = tags;
        break;
      case 'rune':
        runeFilterTags.value = tags;
        break;
    }
  },
});

// 计算当前激活 Tab 的可用标签列表
const activeAvailableTags = computed(() =>
{
  switch (activeTab.value)
  {
    case 'workflow':
      return workflowTags.value;
    case 'tuum':
      return tuumTags.value;
    case 'rune':
      return runeTags.value;
    default:
      return [];
  }
});


// 为所有资源类型创建可用于 v-model 的列表
const workflowsList = computed({
  get: () => workflows.value ? Object.entries(workflows.value).map(([storeId, item]) => ({storeId, item})) : [],
  set: () =>
  {
  }
});
const tuumsList = computed({
  get: () => tuums.value ? Object.entries(tuums.value).map(([storeId, item]) => ({storeId, item})) : [],
  set: () =>
  {
  }
});
const runesList = computed({
  get: () => runes.value ? Object.entries(runes.value).map(([storeId, item]) => ({storeId, item})) : [],
  set: () =>
  {
  }
});

const aggregatedIsLoading = computed(() => isWorkflowsLoading.value || isTuumsLoading.value || isRunesLoading.value);
const aggregatedError = computed(() => workflowsError.value || tuumsError.value || runesError.value);

function executeAll()
{
  executeWorkflows();
  executeTuums();
  executeRunes();
}


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
    content: () => (
        <div>
          {messageLines.map((line, index) => (
              <div key={index}>{line}</div>
          ))}
        </div>
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
    case 'tuum':
      return '枢机';
    case 'rune':
      return '符文';
    default:
      return '资源';
  }
});

const {createNewAction} = useGlobalConfigCreationAction(activeTab, currentTabLabel, emit);

const createButtonRef = ref<InstanceType<typeof NButton> | null>(null);

const handleCreateNew = () =>
{
  const triggerElement = createButtonRef.value?.$el as HTMLElement;

  createNewAction.value.activate(triggerElement);
};


/**
 * vue-draggable-plus 的 :clone prop 处理函数。
 * 在此函数中，我们将原始资源项中的 'data' 部分深拷贝，并递归刷新所有 configId。
 * 注意，从本页面拖拽到编辑中的draggable包裹的列表时，会走:clone prop调用deepCloneWithNewIds，于是configId会自动刷新。
 * 而从本页面到acquireEditSession走的是HTML5的原生路径，不会触发configId的刷新。
 * （如果这引入了什么问题，也可以重新在acquireEditSession中进行修改，目前来看这样最好。）
 * @param {DraggableResourceItem<AnyConfigObject>} originalResourceItem - 原始的资源列表项。
 * @returns {AnyConfigObject | null} - 克隆并刷新 ID 后的纯数据对象，作为拖拽的数据负载。
 */
function handleResourceClone(originalResourceItem: DraggableResourceItem<AnyConfigObject>): AnyConfigObject | null
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
  const storeId = dragEl.dataset.dragId;

  if (type && storeId)
  {
    // 将数据打包成JSON，安全地存入 dataTransfer
    const data = JSON.stringify({type, storeId});
    dataTransfer.setData('text/plain', data);

    // 设置一个自定义类型，用于在 dragenter 事件中进行类型检查。
    // 这个类型本身不携带数据，它的存在就是一个“标记”。
    const customDragType = `application/vnd.workbench.item.${type}`;
    dataTransfer.setData(customDragType, storeId); // 值可以是任意非空字符串，比如 storeId
  }

  dataTransfer.effectAllowed = 'copy'
}

// --- 右键菜单 (Context Menu) 的状态和逻辑 ---
const showDropdown = ref(false);
const dropdownPosition = reactive({x: 0, y: 0});
const activeContextItem = ref<{ type: ConfigType; storeId: string; name: string; isDamaged?: boolean } | null>(null);

// 动态生成菜单选项
const dropdownOptions = computed<DropdownOption[]>(() =>
{
  if (!activeContextItem.value) return [];

  const options: DropdownOption[] = [];

  if (activeContextItem.value.isDamaged)
  {
    options.push({
      label: '强制删除',
      key: 'delete',
      icon: () => (
          <NIcon component={TrashIcon}/>
      )
    });
  }
  else
  {
    options.push({
      label: '编辑',
      key: 'edit',
      icon: () => (
          <NIcon component={EditIcon}/>
      )
    });
    options.push({
      type: 'divider',
      key: 'd1'
    });
    options.push({
      label: '删除',
      key: 'delete',
      icon: () => (
          <NIcon component={TrashIcon}/>
      )
    });
  }
  return options;
});

// 处理从子组件发出的右键事件
function handleContextMenu(payload: { type: ConfigType; storeId: string; name: string; isDamaged?: boolean; event: MouseEvent })
{
  showDropdown.value = false; // 先隐藏任何已存在的菜单
  activeContextItem.value = {...payload};
  dropdownPosition.x = payload.event.clientX;
  dropdownPosition.y = payload.event.clientY;

  nextTick(() =>
  {
    showDropdown.value = true; // 在下一个 DOM 更新周期显示菜单
  });
}

const {switchContext} = useEditorControlPayload();
const {importConfig} = useConfigImportExport();

// 处理菜单项点击
function handleDropdownSelect(key: 'edit' | 'delete')
{
  showDropdown.value = false;
  const item = activeContextItem.value;
  if (!item) return;

  if (key === 'edit')
  {
    switchContext(item.type, item.storeId);
  }
  else if (key === 'delete')
  {
    promptDelete(item.type, item.storeId, item.name);
  }
}

// 弹出删除确认对话框
function promptDelete(type: ConfigType, id: string, name: string)
{
  dialog.warning({
    title: '确认删除',
    content: `你确定要永久删除全局资源 “${name}” 吗？此操作不可恢复。`,
    positiveText: '确定删除',
    negativeText: '取消',
    onPositiveClick: async () =>
    {
      try
      {
        // 调用我们第一步在 store 中添加的方法
        await workbenchStore.deleteGlobalConfig(type, id);
        message.success(`已删除 “${name}”`);
      } catch (error)
      {
        message.error(`删除 “${name}” 失败，请查看控制台获取详情。`);
      }
    },
  });
}

const panelRoot = ref<HTMLDivElement | null>(null);

// 当拖动开始时触发
function onDragStart()
{
  if (!panelRoot.value) return;

  // 创建一个自定义的、可冒泡的 DOM 事件
  const event = new CustomEvent('drag-from-panel:start', {
    bubbles: true, // 这是关键，允许事件向上冒泡
    composed: true // 允许事件跨越 Shadow DOM (可选，但推荐)
  });

  // 从组件的根元素派发事件
  panelRoot.value.dispatchEvent(event);
}

// 当拖动结束时触发
function onDragEnd()
{
  if (!panelRoot.value) return;

  // 派发一个结束事件
  const event = new CustomEvent('drag-from-panel:end', {
    bubbles: true,
    composed: true
  });

  panelRoot.value.dispatchEvent(event);
}

</script>

<style scoped>
.global-resource-panel-wrapper {
  height: 100%;
  display: flex; /* 让子元素 HeaderAndBodyLayout 的 flex:1 生效 */
  flex-direction: column;
}

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

.tag-filter-container {
  margin-top: 12px;
  margin-bottom: 0;
}
</style>