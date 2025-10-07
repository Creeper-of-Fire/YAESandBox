<!-- src/app-workbench/components/panel/GlobalResourcePanel.vue -->
<template>
  <div ref="panelRoot" class="global-resource-panel-wrapper">
    <HeaderAndBodyLayout>
      <template #header>
        <n-flex justify="space-between">
          <n-h4>全局资源</n-h4>
          <div>
            <n-button secondary size="small" style="margin-right: 8px;" @click="handleCreateRootFolder">
              <template #icon>
                <n-icon :component="FolderAddIcon"/>
              </template>
              新建文件夹
            </n-button>
            <InlineInputPopover :action="createNewAction" @confirm="handleCreateNew">
              <n-button secondary size="small" type="primary">
                <template #icon>
                  <n-icon :component="AddIcon"/>
                </template>
                新建
              </n-button>
            </InlineInputPopover>
          </div>
        </n-flex>

        <div v-if="aggregatedIsLoading" class="panel-state-wrapper">
          <n-spin size="small"/>
          <span style="margin-left: 8px;">正在加载...</span>
        </div>
        <div v-else-if="aggregatedError" class="panel-state-wrapper">
          <n-alert :show-icon="true" title="加载错误" type="error">无法加载全局资源。</n-alert>
          <n-button block secondary strong style="margin-top: 12px;" @click="executeAll">重试</n-button>
        </div>

        <n-tabs v-model:value="activeTab" :animated="false" class="global-resource-tabs" justify-content="space-evenly" size="small"
                type="segment">
          <n-tab name="workflow" tab="工作流"/>
          <n-tab name="tuum" tab="枢机"/>
          <n-tab name="rune" tab="符文"/>
        </n-tabs>
      </template>

      <template #body>
        <DraggableTree
            v-if="!isCurrentTreeEmpty"
            v-model="currentTreeData"
            :group="draggableGroupConfig"
            :setData="handleSetData"
            :sort="false"
            item-key="key"
        >
          <template #node="{ node, level, isExpanded, toggleExpand }">
            <!-- 文件夹节点的渲染 -->
            <div v-if="!node.isLeaf" class="folder-node" @click="toggleExpand"
                 @contextmenu.prevent="e => handleContextMenu({ ...node, isFolder: true, event: e, storeId: node.key, name: node.label })">
              <n-icon :class="{ 'is-expanded': isExpanded }" class="folder-arrow">
                <ChevronRightIcon/>
              </n-icon>
              <n-icon class="folder-icon">
                <FolderIcon/>
              </n-icon>
              <span>{{ node.label }}</span>
            </div>
            <!-- 文件节点的渲染 -->
            <div v-else
                 :data-drag-id="node.key"
                 :data-drag-type="node.configType"
            >
              <GlobalResourceListItem
                  :store-id="node.key"
                  :type="node.configType"
                  class="tree-file-item"
                  @contextmenu="(payload) => handleContextMenu({ ...payload, isFolder: false })"
              />
            </div>
          </template>
        </DraggableTree>
        <n-empty v-else :description="`无全局${currentTabLabel}`" class="empty-container" small/>
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

<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NAlert, NButton, NDropdown, NEmpty, NFlex, NH4, NIcon, NSpin, NTab, NTabs, useDialog, useMessage, useThemeVars} from 'naive-ui';
import {AddIcon, AddIcon as FolderAddIcon, ChevronRightIcon, FolderIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {useGlobalResourcePanel} from './useGlobalResourcePanel';
import {createBlankConfig} from '#/utils/createBlankConfig';
import {deepCloneWithNewIds, useWorkbenchStore} from '#/stores/workbenchStore';
import HeaderAndBodyLayout from '#/layouts/HeaderAndBodyLayout.vue';
import GlobalResourceListItem from './GlobalResourceListItem.vue';
import InlineInputPopover from '#/components/share/InlineInputPopover.vue';
import type {EnhancedAction} from "#/composables/useConfigItemActions";
import {useSelectedConfig} from "#/services/editor-context/useSelectedConfig.ts";
import type {FileSystemNode} from "#/stores/useFileSystemStore.ts";
import DraggableTree from "#/components/panel/DraggableTree.vue";
import type {AnyConfigObject, ConfigType} from "#/services/GlobalEditSession.ts";
import type Sortable from 'sortablejs';

const activeTab = ref<'workflow' | 'tuum' | 'rune'>('workflow');

// --- 调用 Composable ---
const {
  aggregatedIsLoading,
  aggregatedError,
  isCurrentTreeEmpty,
  resourceMaps,
  showDropdown,
  dropdownPosition,
  dropdownOptions,
  fileSystemStore,
  executeAll,
  overrideNodeClickBehavior,
  handleDrop,
  handleContextMenu,
  handleDropdownSelect,
  promptCreateFolder,
} = useGlobalResourcePanel({activeTab});

// --- 本地计算属性，用于模板 ---
const workbenchStore = useWorkbenchStore();
const message = useMessage();
const dialog = useDialog();

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

// --- Draggable 配置 ---
const draggableGroupConfig = computed(() =>
{
  // 1. 为每种资源类型定义一个固定的、全局唯一的 group 名称
  const groupNames = {
    workflow: 'workflows-group', // 与 CollapsibleConfigList 中 workflow 的 group 匹配
    tuum: 'tuums-group',         // 与 CollapsibleConfigList 中 tuum 的 group 匹配
    rune: 'runes-group',         // 与 CollapsibleConfigList 中 rune 的 group 匹配
  };

  const currentGroupName = groupNames[activeTab.value];

  return {
    name: currentGroupName,
    /**
     * ✨ 函数式 pull ✨
     * @param to - 目标 Sortable 实例
     * @param from - 源 Sortable 实例
     * @returns 'clone' | boolean - 返回 'clone' 表示克隆，返回 true 表示允许移动
     */
    pull: (to: Sortable, from: Sortable): 'clone' | boolean => {
      // 获取目标 group 的名称
      const toGroup = to.options.group;

      const toGroupName = toGroup && typeof toGroup === 'object' ? toGroup.name : toGroup;

      // 如果目标 group 的名称与当前 group 名称不同，
      // 说明我们正在拖拽到外部（例如编辑器面板），此时执行克隆。
      if (toGroupName !== currentGroupName) {
        return 'clone';
      }

      // 否则，说明是在内部移动（排序或移动到子文件夹），
      // 此时返回 true，允许移动。
      return true;
    },
    /**
     * ✨ 智能的 put 函数 ✨
     * @param to   - 目标 Sortable 实例
     * @param from - 源 Sortable 实例
     * @returns boolean - 是否允许放置
     */
    put: (to: Sortable, from: Sortable): boolean => {
      const fromGroup = from.options.group;

      const fromGroupName = fromGroup && typeof fromGroup === 'object' ? fromGroup.name : fromGroup;

      if (!fromGroupName) {
        return false;
      }

      return fromGroupName === currentGroupName;
    }
  };
});


// v-model 绑定到当前激活的树数据
const currentTreeData = computed({
  get: () => fileSystemStore.getTreeForType(activeTab.value) || [],
  set: (value) =>
  {
    // 当 DraggableTree 通过 v-model 更新数据时，
    // 我们调用 store 的 action 来持久化这个改变。
    fileSystemStore.updateTree(activeTab.value, value);
  }
});

// 处理节点移动事件（来自 DraggableTree 的冒泡）
function handleNodeMoved(payload: any)
{
  // 这里的逻辑可以用来通知后端或执行其他副作用
  // 目前我们的数据模型是 v-model 驱动的，所以状态已经更新了
  console.log('Node moved:', payload);
}

const handleCreateRootFolder = () =>
{
  promptCreateFolder(activeTab.value);
};

// --- “新建”按钮相关逻辑 ---
const runeTypeOptions = computed(() =>
{
  const schemas = workbenchStore.runeSchemasAsync.state;
  if (!schemas) return [];
  return Object.keys(schemas).map(key => ({
    label: workbenchStore.runeMetadata[key]?.classLabel || schemas[key].title || key,
    value: key,
  }));
});

const runeDefaultNameGenerator = (newType: string, options: any[]) =>
{
  if (newType)
  {
    const schema = workbenchStore.runeSchemasAsync.state[newType];
    const defaultName = schema?.properties?.name?.default;
    if (typeof defaultName === 'string')
    {
      return defaultName;
    }
    return options.find(opt => opt.value === newType)?.label || '新符文';
  }
  return '';
};

/**
 * :content-type="activeTab === 'rune' ? 'select-and-input' : 'input'"
 *             :default-name-generator="runeDefaultNameGenerator"
 *             :initial-value="`新建${currentTabLabel}`"
 *             :input-placeholder="`请输入新的${currentTabLabel}名称`"
 *             :select-options="runeTypeOptions"
 *             :select-placeholder="'请选择符文类型'"
 *             :title="`新建全局${currentTabLabel}`"
 */
const createNewAction = computed<EnhancedAction>(() =>
    ({
      key: 'create-new-global',
      icon: AddIcon,
      label: '新建全局配置',
      renderType: 'button',
      disabled: false,
      popoverContentType: activeTab.value === 'rune' ? 'select-and-input' : 'input',
      popoverDefaultNameGenerator: runeDefaultNameGenerator,
      popoverInitialValue: `新建${currentTabLabel.value}`,
      popoverInputPlaceholder: `请输入新的${currentTabLabel.value}名称`,
      popoverSelectOptions: runeTypeOptions.value,
      popoverSelectPlaceholder: '请选择符文类型',
      popoverTitle: `新建全局${currentTabLabel.value}`,
      onConfirm: handleCreateNew
    })
)
const {switchContext} = useSelectedConfig();

async function handleCreateNew(payload: { name?: string; type?: string })
{
  const {name, type: runeType} = payload;
  if (!name)
    return message.error('请输入名称');

  const resourceType: "workflow" | "tuum" | "rune" = activeTab.value;
  if (resourceType === 'rune' && !runeType)
    return message.error('请选择符文类型');

  try
  {
    const blankConfig =
        resourceType === 'rune'
            ? await createBlankConfig('rune', name, {runeType: runeType!})
            : resourceType === 'workflow'
                ? await createBlankConfig('workflow', name)
                : await createBlankConfig('tuum', name);

    const newSession = workbenchStore.createNewDraftSession(resourceType, blankConfig);
    await switchContext(newSession.type, newSession.storeId);
  } catch (e: any)
  {
    message.error(`创建失败: ${e.message}`);
  }
}

function handleSetData(dataTransfer: DataTransfer, dragEl: HTMLElement)
{
  const type = dragEl.dataset.dragType as ConfigType | 'folder' | undefined;
  const storeId = dragEl.dataset.dragId;

  // 只为 "文件" 节点设置 dataTransfer 数据，用于拖放到外部编辑器
  if (type && type !== 'folder' && storeId)
  {
    const data = JSON.stringify({type, storeId});
    dataTransfer.setData('text/plain', data);

    // 设置自定义类型，用于拖放目标区域的类型检查
    const customDragType = `application/vnd.workbench.item.${type}`;
    dataTransfer.setData(customDragType, storeId);
  }
  dataTransfer.effectAllowed = 'copy';
}

const themeVars = useThemeVars();
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

.folder-node {
  display: flex;
  align-items: center;
  padding: 6px 8px;
  cursor: pointer;
  border-radius: 4px;
  user-select: none;
}

.folder-node:hover {
  background-color: v-bind('themeVars.hoverColor');
}

.folder-arrow {
  margin-right: 4px;
  transition: transform 0.2s ease-in-out;
}

.folder-arrow.is-expanded {
  transform: rotate(90deg);
}

.folder-icon {
  margin-right: 8px;
  color: v-bind('themeVars.primaryColor');
}

.tree-file-item {
  /* 确保文件项有拖拽手柄 */

  :deep(.drag-handle) {
    display: flex !important; /* 覆盖可能隐藏它的样式 */
    cursor: grab;
    align-items: center;
    justify-content: center;
    width: 24px;
    /* 重命名拖拽手柄的 class 以避免冲突 */
    /* 我们在 DraggableTree 中使用了 .drag-handle-tree */
  }
}

:deep(.drag-handle-tree) {
  cursor: grab;
}
</style>