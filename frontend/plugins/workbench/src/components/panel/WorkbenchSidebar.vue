<!-- src/app-workbench/components/.../WorkbenchSidebar.vue -->
<template>
  <!-- 1. 根容器始终存在，并监听拖拽事件 -->
  <div
      class="workbench-sidebar"
      @dragenter="handleDragEnter"
      @dragover.prevent
  >
    <!-- 2. 根据 session 是否存在，渲染不同的内部视图 -->
    <HeaderAndBodyLayout v-if="session && session.getData().value">
      <template #header>
        <n-h4 class="sidebar-title-bar">
          <span class="title-text" @click="selectCurrentSessionItem">{{ currentConfigName }}</span>

          <n-popover trigger="hover">
            <template #trigger>
              <!-- 只有当编辑的是工作流时才显示此按钮 -->
              <n-button
                  v-if="session.type === 'workflow'"
                  style="font-size: 20px;"
                  text
                  @click="selectCurrentSessionItem"
              >
                <n-icon :component="GraphIcon"/>
              </n-button>
            </template>
            进入图编辑视图
          </n-popover>

          <n-popover trigger="hover">
            <template #trigger>
              <n-button style="font-size: 20px;" text @click="handleClose">
                <n-icon :component="CloseIcon"/>
              </n-button>
            </template>
            关闭编辑视图
          </n-popover>
        </n-h4>


        <n-flex class="action-bar" justify="space-between">
          <!-- 左侧：动态生成的上下文操作 -->
          <n-flex>
            <template v-for="action in getItemActions()" :key="action.key">
              <!-- 渲染需要 Popover 的动作 (如重命名、添加) -->
              <InlineInputPopover v-if="action.renderType === 'popover'"
                                  :action="action"
                                  @confirm="payload => action.handler?.(payload)"
              >
                <n-button
                    :disabled="action.disabled"
                    :type="action.type || 'default'"
                    size="small"
                    strong
                >
                  {{ action.label }}
                </n-button>
              </InlineInputPopover>
              <!-- 渲染简单按钮 (未来可能用到) -->
              <n-button
                  v-else
                  :disabled="action.disabled"
                  :type="action.type || 'default'"
                  size="small"
                  strong
                  @click="action.handler?.({})"
              >
                {{ action.label }}
              </n-button>
            </template>
          </n-flex>
          <n-flex>
            <n-button :disabled="!isDirty" secondary size="small" strong type="error" @click="handleDiscard">放弃</n-button>
            <n-button :disabled="!isDirty" secondary size="small" strong type="success" @click="handleSave">保存</n-button>
          </n-flex>
        </n-flex>
        <!-- 分割线，让布局更清晰 -->
        <n-divider style="margin-top: 12px; margin-bottom: 12px;"/>
      </template>


      <!-- 2b. 可滚动的内容区域 -->
      <template #body>
        <template v-if="session.type === 'workflow' && workflowData">
          <p class="sidebar-description">拖拽全局枢机到枢机列表，或将全局资源拖到此区域的任意位置以替换当前编辑项。</p>
          <WorkflowItemRenderer :workflow="workflowData"/>
        </template>

        <template v-else-if="session.type === 'tuum' && tuumData">
          <p class="sidebar-description">拖拽全局符文到符文列表，或将全局资源拖到此区域的任意位置以替换当前编辑项。</p>
          <TuumItemRenderer
              :is-collapsible="false"
              :is-draggable="false"
              :parent-workflow="null"
              :tuum="tuumData"
              style="margin-top: 16px"
          />
        </template>

        <template v-else-if="session.type === 'rune' && runeData">
          <RuneItemRenderer
              :parent-tuum="null"
              :rune="runeData"/>
        </template>
      </template>
    </HeaderAndBodyLayout>
    <div v-else class="empty-state-wrapper">
      <div class="custom-empty-state">
        <n-icon :component="AddBoxIcon" :size="80"/>
        <p class="description">从全局资源区拖拽一项到此处开始编辑</p>
      </div>
    </div>


    <!-- 3. 拖拽覆盖层，覆盖在内容或空状态之上 -->
    <div
        v-if="isDragOverContainer"
        class="drop-overlay"
        @dragleave="handleDragLeave"
        @drop.prevent="handleDrop"
        @dragover.prevent
    >
      <div class="drop-overlay-content">
        <n-icon :component="SwapHorizIcon" size="48"/>
        <p>释放鼠标以{{ session ? '替换当前编辑项' : '开始新的编辑' }}</p>
      </div>
    </div>
  </div>
</template>


<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NH4, NIcon, useDialog, useMessage, useThemeVars} from 'naive-ui';
import type {ConfigType} from "#/services/GlobalEditSession.ts";
import type {AbstractRuneConfig, TuumConfig, WorkflowConfig} from "#/types/generated/workflow-config-api-client";
import TuumItemRenderer from '../tuum/TuumItemRenderer.vue';
import WorkflowItemRenderer from "#/components/workflow/WorkflowItemRenderer.vue";
import {AddBoxIcon, CloseIcon, SwapHorizIcon, GraphIcon} from '@yaesandbox-frontend/shared-ui/icons';
import HeaderAndBodyLayout from "#/layouts/HeaderAndBodyLayout.vue";
import {useConfigItemActions} from "#/composables/useConfigItemActions.ts";
import InlineInputPopover from "#/components/share/InlineInputPopover.vue";
import RuneItemRenderer from "#/components/rune/RuneItemRenderer.vue";
import {useSelectedConfig} from "#/services/editor-context/useSelectedConfig.ts";

const props = defineProps<{}>();

const emit = defineEmits<{
  (e: 'start-editing', payload: { type: ConfigType; id: string }): void;
  (e: 'close-session', payload: { id: string }): void;
}>();

const dialog = useDialog();
const message = useMessage();

// 工作流点击
// TODO之后转移到工作流内部
const {selectedContext, updateSelectedConfig, activeContext} = useSelectedConfig();
const session = computed(() => activeContext?.value?.session);

function selectCurrentSessionItem()
{
  if (session.value)
  {
    const sessionData = session.value.getData().value;
    if (sessionData)
    {
      updateSelectedConfig(sessionData);
    }
  }
}

// --- 状态 ---
// 覆盖层的显隐状态
const isDragOverContainer = ref(false);

// --- 计算属性 ---
// 通过 session 获取 isDirty 状态
const isDirty = computed(() => session.value?.getIsDirty().value ?? false);

// --- 按钮事件处理 ---

async function handleSave()
{
  if (!session.value) return;

  const result = await session.value.save();
  if (result.success)
  {
    message.success(`“${result.name}” 已保存!`);
  }
  else
  {
    const error = result.error;
    // 捕获 GlobalEditSession.save 抛出的错误
    message.error(`保存失败：${result.name}，请检查内容或查看控制台。`);
    console.error(`${result.name} 保存失败，详情:`, error);
  }

}

function handleDiscard()
{
  if (!session.value) return;
  dialog.warning({
    title: '放弃更改',
    content: '您确定要放弃所有未保存的更改吗？此操作不可撤销。',
    positiveText: '确定放弃',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      session.value?.discard();
      message.info("更改已放弃。");
      // 放弃后，数据会恢复原状，但会话依然存在。
    },
  });
}

function handleClose()
{
  if (!session.value)
    return
  emit('close-session', {id: session.value.globalId});
}

// --- 从 composable 获取动作 ---
const {getActions: getItemActions} = useConfigItemActions({
  itemRef: computed(() => session.value?.getData().value ?? null),
  parentContextRef: ref(null), // 侧边栏是顶级编辑，没有父级
});
// --- 等级定义和比较逻辑 ---

// 定义配置类型的等级
const typeHierarchy: Record<ConfigType, number> = {
  workflow: 3,
  tuum: 2,
  rune: 1,
};

/**
 * 从 DragEvent 中解析出我们自定义的拖拽类型。
 * @param event - 拖拽事件
 * @returns 拖拽的 ConfigType 或 null
 */
function getDraggedItemType(event: DragEvent): ConfigType | null
{
  for (const type of event.dataTransfer?.types ?? [])
  {
    const match = type.match(/^application\/vnd\.workbench\.item\.(workflow|tuum|rune)$/);
    if (match)
    {
      return match[1] as ConfigType;
    }
  }
  return null;
}


/**
 * 当拖拽项首次进入容器时，进行等级判断。
 */
function handleDragEnter(event: DragEvent)
{
  const draggedType = getDraggedItemType(event);

  // 如果无法识别拖拽类型，则不响应该拖拽
  if (!draggedType)
  {
    return;
  }

  // 如果当前没有编辑会话，任何可识别的拖拽都应该显示覆盖层
  if (!session.value)
  {
    isDragOverContainer.value = true;
    return;
  }

  // 获取当前会话和拖拽物的等级
  const currentSessionType = session.value.type;
  const draggedLevel = typeHierarchy[draggedType];
  const currentLevel = typeHierarchy[currentSessionType];

  // *** 核心判断逻辑 ***
  // 只有当拖拽项的等级 >= 当前编辑项的等级时，才显示“替换”覆盖层。
  // 否则，不显示覆盖层，让事件“穿透”到下面的 draggable 区域。
  if (draggedLevel >= currentLevel)
  {
    isDragOverContainer.value = true;
  }
}


/**
 * 当拖拽项离开覆盖层时，隐藏它。
 */
function handleDragLeave()
{
  isDragOverContainer.value = false;
}

/**
 * 在覆盖层上完成放置操作。
 */
function handleDrop(event: DragEvent)
{
  isDragOverContainer.value = false;
  if (event.dataTransfer)
  {
    try
    {
      const dataString = event.dataTransfer.getData('text/plain');
      if (dataString)
      {
        const {type, id} = JSON.parse(dataString);
        if (type && id)
        {
          emit('start-editing', {type, id});
        }
      }
    } catch (e)
    {
      console.error("解析拖拽数据失败:", e);
    }
  }
}

// --- 为不同编辑类型创建独立的计算属性，使模板更清晰 ---
const workflowData = computed(() =>
    session.value?.type === 'workflow' ? session.value.getData().value as WorkflowConfig : null
);
const tuumData = computed(() =>
    session.value?.type === 'tuum' ? session.value.getData().value as TuumConfig : null
);
const runeData = computed(() =>
    session.value?.type === 'rune' ? session.value.getData().value as AbstractRuneConfig : null
);
const currentConfigName = computed(() =>
{
  if (!session.value) return ''; // 如果没有会话，返回空字符串
  if (session.value.type === 'workflow' && workflowData.value) return `工作流: ${workflowData.value.name}`;
  if (session.value.type === 'tuum' && tuumData.value) return `枢机: ${tuumData.value.name}`;
  if (session.value.type === 'rune' && runeData.value) return `符文: ${runeData.value.name}`;
  return '未知';
});

const themeVars = useThemeVars();
</script>

<style scoped>
/* 根容器必须是 relative 并且占满高度 */
.workbench-sidebar {
  position: relative;
  height: 100%;
  box-sizing: border-box;
  display: flex; /* 使用 flex 布局让内部内容撑开 */
  flex-direction: column;
}

.title-text {
  /* 防止长标题把关闭按钮挤走 */
  flex-grow: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  margin-right: 8px; /* 和关闭按钮之间留点空隙 */
  cursor: pointer;
}

.sidebar-title-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.action-bar {
  margin-top: 8px;
}

.empty-state-wrapper {
  flex-grow: 1;
  width: 100%;
  height: 100%;
  display: flex;
  justify-content: center;
  align-items: center;
  border: 2px dashed v-bind('themeVars.borderColor');
  border-radius: 8px;
  box-sizing: border-box;
  background-color: v-bind('themeVars.hoverColor');
  padding: 20px;
}

/*
  - 自定义空状态的样式
  - 可以自由调整图标和文字的样式
*/
.custom-empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px; /* 图标和文字的间距 */
  color: v-bind('themeVars.textColor3'); /* 图标的颜色，比较柔和 */
  text-align: center;
  pointer-events: none; /* 防止它干扰拖拽事件 */
}

.custom-empty-state .description {
  font-size: 16px; /* 加大字体 */
  font-weight: 500;
  color: v-bind('themeVars.textColor2'); /* 文字的颜色 */
  max-width: 220px; /* 控制文字宽度，使其在必要时换行 */
  line-height: 1.5;
}

.sidebar-description {
  color: v-bind('themeVars.textColor2');
  font-size: 13px;
  margin-top: 8px;
  margin-bottom: 16px;
}

.drop-overlay {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-color: v-bind('themeVars.primaryColorSuppl');
  border: 2px dashed v-bind('themeVars.primaryColor');
  border-radius: 6px;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: center;
  box-sizing: border-box;
}

.drop-overlay-content {
  text-align: center;
  color: v-bind('themeVars.primaryColor');
  font-weight: 500;
  pointer-events: none;
}
</style>