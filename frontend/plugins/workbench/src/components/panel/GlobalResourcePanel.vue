<!-- src/app-workbench/components/.../GlobalResourcePanel.vue -->
<template>
  <div class="global-resource-panel-wrapper">
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
          <n-tab
              v-for="config in tabConfigs"
              :key="config.type"
              :name="config.type"
              :tab="config.label"
          />
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
        <draggable
            v-if="activeList.length > 0"
            v-model="draggableList"
            :animation="150"
            :clone="handleResourceClone"
            :group="{ name: currentTabConfig.dragGroupName, pull: 'clone', put: false }"
            :setData="setDataHandler"
            :sort="false"
            class="resource-list"
            item-key="storeId"
            @end="onDragEnd"
            @start="onDragStart"
        >
          <div v-for="element in activeList"
               :key="element.storeId"
               :data-drag-id="element.storeId"
               :data-drag-type="currentTabConfig.type"
          >
            <GlobalResourceListItem
                :store-id="element.storeId"
                :type="currentTabConfig.type"
                @contextmenu="handleContextMenu"
                @show-error-detail="showErrorDetail"
            />
          </div>
        </draggable>
        <n-empty
            v-else
            :description="currentTabConfig.emptyDescription"
            class="empty-container"
            small
        />
      </template>
    </HeaderAndBodyLayout>

    <ContextMenu/>
  </div>
</template>


<script lang="tsx" setup>
import {computed, ref} from 'vue';
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
import {useConfigImportExport} from "#/composables/useConfigImportExport.ts";
import {useResourceDragProvider} from "#/composables/useResourceDragAndDrop.tsx";
import {useContextMenu} from "@yaesandbox-frontend/shared-ui";
import {useMobileDragCoordinator} from "#/composables/useMobileDragCoordinator.ts";

interface TabConfig
{
  type: ConfigType;
  label: string;
  dragGroupName: string;
  emptyDescription: string;
}

const tabConfigs: TabConfig[] = [
  {type: 'workflow', label: '工作流', dragGroupName: 'workflows-group', emptyDescription: '无全局工作流'},
  {type: 'tuum', label: '枢机', dragGroupName: 'tuums-group', emptyDescription: '无全局枢机'},
  {type: 'rune', label: '符文', dragGroupName: 'runes-group', emptyDescription: '无全局符文'},
];

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

const resourceHandlers = {
  workflow: useFilteredGlobalResources('workflow'),
  tuum: useFilteredGlobalResources('tuum'),
  rune: useFilteredGlobalResources('rune'),
};

// 可写的计算属性，用于 v-model 绑定到 n-select
const activeFilterTags = computed({
  get: () => activeResourceHandler.value.filterTags.value,
  set: (tags: string[]) => { activeResourceHandler.value.filterTags.value = tags; }
});

// 计算当前激活 Tab 的可用标签列表
const activeAvailableTags = computed(() => activeResourceHandler.value.allAvailableTags.value);

// 获取当前激活标签页的数据处理器
const activeResourceHandler = computed(() =>
{
  return resourceHandlers[activeTab.value];
});

const activeList = computed(() =>
{
  const resources = activeResourceHandler.value.resources.value;
  return resources ? Object.entries(resources).map(([storeId, item]) => ({storeId, item})) : [];
});

const draggableList = computed({
  get: () => activeList.value,
  // 即使我们不排序，v-model 也需要一个 set 函数来避免 Vue 警告
  set: () => {}
});

// --- 聚合状态 ---
const aggregatedIsLoading = computed(() =>
    Object.values(resourceHandlers).some(h => h.isLoading.value)
);
const aggregatedError = computed(() =>
    Object.values(resourceHandlers).find(h => h.error.value)?.error.value ?? null
);

// --- 聚合方法 ---
function executeAll()
{
  Object.values(resourceHandlers).forEach(h => h.execute());
}

// 获取当前激活标签页的完整配置
const currentTabConfig = computed(() =>
{
  return tabConfigs.find(c => c.type === activeTab.value)!;
});

/**
 * 计算属性，用于在 Tooltip 中显示当前激活的标签页名称
 */
const currentTabLabel = computed(() => currentTabConfig.value.label);

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

const {createNewAction} = useGlobalConfigCreationAction(
    activeTab,
    currentTabLabel,
    (newSession) =>
    {
      emit('start-editing', newSession);
    }
);

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

const {setDataHandler} = useResourceDragProvider();

const {notifyDragStart, notifyDragEnd} = useMobileDragCoordinator();

// 当拖动开始时触发
function onDragStart()
{
  notifyDragStart();
  // 可以添加更多处理逻辑
}

// 当拖动结束时触发
function onDragEnd()
{
  notifyDragEnd();
  // 可以添加更多处理逻辑
}

// --- 右键菜单 (Context Menu) 的状态和逻辑 ---
const activeContextItem = ref<{ type: ConfigType; storeId: string; name: string; isDamaged?: boolean } | null>(null);

// 动态生成菜单选项
const dropdownOptions = computed<DropdownOption[]>(() =>
{
  if (!activeContextItem.value) return [];

  const item = activeContextItem.value;

  const editAction = {
    label: '编辑',
    key: 'edit',
    icon: () => <NIcon component={EditIcon}/>,
    onClick: () =>
    {
      switchContext(item.type, item.storeId);
    }
  };

  const deleteAction = {
    label: item.isDamaged ? '强制删除' : '删除',
    key: 'delete',
    icon: () => <NIcon component={TrashIcon}/>,
    onClick: () =>
    {
      promptDelete(item.type, item.storeId, item.name);
    }
  };

  if (item.isDamaged)
  {
    return [deleteAction];
  }
  else
  {
    return [
      editAction,
      {type: 'divider', key: 'd1'},
      deleteAction
    ];
  }
});

const {showMenu, ContextMenu} = useContextMenu(dropdownOptions);

// 处理从子组件发出的右键事件
function handleContextMenu(payload: { type: ConfigType; storeId: string; name: string; isDamaged?: boolean; event: MouseEvent })
{
  activeContextItem.value = {...payload};
  showMenu(payload.event);
}

const {switchContext} = useEditorControlPayload();
const {importConfig} = useConfigImportExport();

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