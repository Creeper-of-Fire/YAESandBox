<!-- src/app-workbench/components/.../WorkbenchSidebar.vue -->
<template>
  <!-- 1. 根容器始终存在，并监听拖拽事件 -->
  <DropZoneContainer
      class="workbench-sidebar"
  >
    <!-- 2. 根据 session 是否存在，渲染不同的内部视图 -->
    <HeaderAndBodyLayout v-if="session && session.getData().value">
      <template #header>
        <n-h4 class="sidebar-title-bar">
          <span class="title-text" @click="selectCurrentSessionItem">{{ currentConfigName }}</span>

          <div v-if="session.type === 'workflow'" class="view-switch-bar">
            <n-button
                block
                secondary
                strong
                type="primary"
                @click="selectCurrentSessionItem"
            >
              <template #icon>
                <n-icon :component="GraphIcon"/>
              </template>
              进入图编辑视图
            </n-button>
          </div>

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
            <!-- 简单的 v-for 循环，为每个 action 创建一个按钮 -->
            <n-button
                v-for="action in itemActions"
                :key="action.key"
                :disabled="action.disabled"
                :type="action.type || 'default'"
                size="small"
                strong
                @click="action.activate($event.currentTarget as HTMLElement)"
            >
              <!-- 按钮的图标和文本直接来自 action -->
              <template v-if="action.icon" #icon>
                <n-icon :component="action.icon"/>
              </template>
              {{ action.label }}
            </n-button>
          </n-flex>

          <n-flex v-if="!isReadOnly">
            <n-button :disabled="!fullDraft" secondary size="small" strong type="info" @click="handleExport">导出</n-button>
            <n-button :disabled="!isDirty" secondary size="small" strong type="error" @click="handleDiscard">放弃</n-button>
            <n-button :disabled="!isDirty" secondary size="small" strong type="success" @click="handleSave">保存</n-button>
          </n-flex>
        </n-flex>
        <!-- 分割线，让布局更清晰 -->
        <n-divider style="margin-top: 12px; margin-bottom: 12px;"/>
      </template>


      <!-- 2b. 可滚动的内容区域 -->
      <template #body>
        <ConfigMetadataEditor v-if="fullDraft" :draft="fullDraft"/>

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
    <DropZoneOverlay>
      <div class="drop-overlay-content">
        <n-icon :component="SwapHorizIcon" size="48"/>
        <p>释放鼠标以{{ session ? '替换当前编辑项' : '开始新的编辑' }}</p>
      </div>
    </DropZoneOverlay>
  </DropZoneContainer>
</template>


<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NH4, NIcon, useDialog, useMessage, useThemeVars} from 'naive-ui';
import type {AbstractRuneConfig, TuumConfig, WorkflowConfig} from "#/types/generated/workflow-config-api-client";
import TuumItemRenderer from '../tuum/TuumItemRenderer.vue';
import WorkflowItemRenderer from "#/components/workflow/WorkflowItemRenderer.vue";
import {AddBoxIcon, CloseIcon, GraphIcon, SwapHorizIcon} from '@yaesandbox-frontend/shared-ui/icons';
import HeaderAndBodyLayout from "#/layouts/HeaderAndBodyLayout.vue";
import {useConfigItemActions} from "#/components/share/itemActions/useConfigItemActions.tsx";
import RuneItemRenderer from "#/components/rune/RuneItemRenderer.vue";
import {useSelectedConfig} from "#/services/editor-context/useSelectedConfig.ts";
import ConfigMetadataEditor from "#/components/share/ConfigMetadataEditor.vue";
import {useConfigImportExport} from "#/composables/useConfigImportExport.ts";
import {type DragPayload, useResourceDropZone} from "#/composables/useResourceDragAndDrop.tsx";

const props = defineProps<{}>();

const emit = defineEmits<{
  (e: 'close-session', payload: { storeId: string }): void;
}>();

const dialog = useDialog();
const message = useMessage();

// 工作流点击
// TODO之后转移到工作流内部
const {switchContext, selectedContext, isReadOnly, updateSelectedConfig, activeContext} = useSelectedConfig();
const session = computed(() => activeContext?.value?.session);
const fullDraft = computed(() => session.value?.getFullDraft().value ?? null);

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
  emit('close-session', {storeId: session.value.storeId});
}

// --- 从 composable 获取动作 ---
const {actions: itemActions} = useConfigItemActions({
  itemRef: computed(() => session.value?.getData().value ?? null),
  parentContextRef: ref(null), // 侧边栏是顶级编辑，没有父级
});

// 定义当 drop 发生时应该执行的逻辑
const handleDropLogic = (payload: DragPayload) =>
{
  switchContext(payload.type, payload.storeId);
};

const {DropZoneContainer, DropZoneOverlay} = useResourceDropZone({
  session,
  onDrop: handleDropLogic,
});

const {exportConfig} = useConfigImportExport();

function handleExport()
{
  if (fullDraft.value)
  {
    exportConfig(fullDraft.value);
  }
  else
  {
    message.error("没有可导出的活动配置。");
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
  position: relative; /* 必须是 relative，为 overlay 提供定位上下文 */
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

.drop-overlay-content {
  text-align: center;
  color: v-bind('themeVars.primaryColor');
  font-weight: 500;
  pointer-events: none;/* 内部内容不响应鼠标事件 */
}
</style>