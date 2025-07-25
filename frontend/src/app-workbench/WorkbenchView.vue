<!-- 文件路径: src/app-workbench/WorkbenchView.vue -->
<template>
  <div class="workbench-view">
    <!-- 1. 顶部控制栏 -->
    <div class="workbench-header">
      <!-- 左侧：标题和紧邻的开关 -->
      <div class="header-left-group">
        <n-h3 style="margin: 0;">工作流编辑器</n-h3>
        <!-- 使用 v-model:value 来正确绑定 n-switch -->
        <n-switch v-model:value="isGlobalPanelVisible" size="large">
          <template #checked>
            全局资源 (开)
          </template>
          <template #unchecked>
            全局资源 (关)
          </template>
        </n-switch>

        <n-switch v-model:value="isEditorPanelVisible" size="large">
          <template #checked>
            当前编辑 (开)
          </template>
          <template #unchecked>
            当前编辑 (关)
          </template>
        </n-switch>
      </div>

      <!-- 右侧的全局操作按钮 -->
      <n-space class="header-right-controls">
        <n-button
            :disabled="!workbenchStore.hasDirtyDrafts" :loading="isSavingAll" secondary
            strong
            type="primary"
            @click="handleSaveAll"
        >
          <template #icon>
            <n-icon :component="SaveIcon"/>
          </template>
          全部保存
        </n-button>
        <n-button @click="showAiConfigModal = true">全局AI配置</n-button>
      </n-space>
    </div>

    <!-- 2. 编辑器核心布局 -->
    <EditorLayout
        :is-editor-panel-visible="isEditorPanelVisible"
        :is-global-panel-visible="isGlobalPanelVisible"
        class="editor-layout"
    >
      <!-- 2a. 全局资源面板插槽 -->
      <template #global-panel>
        <GlobalResourcePanel @start-editing="handleStartEditing"/>
      </template>

      <!-- 2b. 当前编辑结构插槽 -->
      <template #editor-panel>
        <WorkbenchSidebar
            :key="activeSession?.globalId ?? 'empty-session'"
            :session="activeSession"
            @closeSession="handleCloseSession"
            @start-editing="handleStartEditing"
        />
      </template>

      <!-- 2c. 主内容区插槽 -->
      <template #module-panel>
        <MainEditPanel/>
      </template>
    </EditorLayout>

    <!-- 3. 临时：承载 AI 配置面板的模态框 -->
    <n-modal
        v-model:show="showAiConfigModal"
        :bordered="false"
        :closable="true"
        :mask-closable="false"
        preset="card"
        style="width: 90%; max-width: 1400px; height: 90vh;"
        title="全局 AI 配置中心"
    >
      <div style="height: calc(90vh - 100px); overflow-y: auto;">
        <AiConfigEditorPanel/>
      </div>
    </n-modal>
  </div>
</template>

<script lang="ts" setup>
import {onBeforeUnmount, onMounted, provide, ref} from 'vue';
import {NButton, NH3, NSwitch, useDialog, useMessage} from 'naive-ui';
import {SaveIcon} from '@/utils/icons';
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore';
import {type ConfigType, type EditSession} from '@/app-workbench/services/EditSession';

import EditorLayout from '@/app-workbench/layouts/EditorLayout.vue';
import GlobalResourcePanel from '@/app-workbench/components/panel/GlobalResourcePanel.vue';
import WorkbenchSidebar from '@/app-workbench/components/panel/WorkbenchSidebar.vue';
import AiConfigEditorPanel from "@/app-workbench/features/ai-config-panel/AiConfigEditorPanel.vue";
import MainEditPanel from "@/app-workbench/components/panel/MainEditPanel.vue";
import {type SelectedConfigItem, SelectedConfigItemKey} from "@/app-workbench/utils/injectKeys.ts";
import type {AbstractModuleConfig} from "@/app-workbench/types/generated/workflow-config-api-client";
import type {StepEditorContext} from "@/app-workbench/components/step/editor/StepEditorContext.ts";
import type {ModuleEditorContext} from "@/app-workbench/components/module/editor/ModuleEditorContext.ts";

defineOptions({
  name: 'WorkbenchView'
});

const selectedConfig = ref<StepEditorContext | ModuleEditorContext | null>(null);

provide<SelectedConfigItem>(SelectedConfigItemKey, {
  data: selectedConfig,
  update: (config) =>
  {
    selectedConfig.value = config;
  }
});


// TODO 使用vueuse的useStore来存储单例的UI状态

const workbenchStore = useWorkbenchStore();
const message = useMessage();
const dialog = useDialog();

// --- 核心状态 ---
const activeSession = ref<EditSession | null>(null);


// 用于“正在请求新编辑视图”的加载状态
const isAcquiringSession = ref(false);
// 用于“全部保存”按钮的加载状态
const isSavingAll = ref(false);

// --- UI 控制状态 ---
const isGlobalPanelVisible = ref(true);
const isEditorPanelVisible = ref(true);
const showAiConfigModal = ref(false);

/**
 * *** 处理全局保存操作 ***
 */
async function handleSaveAll()
{
  isSavingAll.value = true;
  const {saved, failed} = await workbenchStore.saveAllDirtyDrafts();
  isSavingAll.value = false;

  if (failed.length > 0)
  {
    // 如果有失败项，显示错误对话框
    // 从 failed 结果中直接获取名字
    const failedNames = failed.map(item =>
    {
      // 从 store 中安全地获取最新的名字，因为保存失败，草稿还在
      return item.name || item.id;
    }).join('、');

    dialog.error({
      title: '部分保存失败',
      content: `以下配置项未能成功保存：\n${failedNames}\n\n请检查控制台获取详细错误信息。`,
      positiveText: '好的'
    });
    // 仍然在控制台打印详细信息以供调试
    console.error("保存失败详情:", failed);
  }

  if (saved.length > 0)
  {
    // 即使部分失败，也要提示成功的部分
    message.success(`成功保存 ${saved.length} 项更改！`);
  }

  if (saved.length === 0 && failed.length === 0)
  {
    // 处理没有脏数据可保存的情况
    message.info("没有需要保存的更改。");
  }
}

/**
 * *** 处理关闭会话的请求 ***
 */
function handleCloseSession()
{
  activeSession.value = null;
}

/**
 * 处理从左侧面板点击“编辑”按钮或双击列表项的事件。或者其他的“开始编辑”事件
 * @param {object} payload - 包含类型和ID的对象。
 */
async function handleStartEditing({type, id: globalId}: { type: ConfigType; id: string })
{
  if (isAcquiringSession.value) return;

  // 如果点击的是当前已激活的会话，则根据类型辅助性地打开面板，然后直接返回
  if (activeSession.value && activeSession.value.globalId === globalId)
  {
    if (type !== 'module')
    {
      isEditorPanelVisible.value = true;
    }
    return;
  }

  // 切换新会话前，重置UI状态
  selectedConfig.value = null;
  isAcquiringSession.value = true;
  activeSession.value = null; // 先清空，让UI显示加载状态

  try
  {
    const session = await workbenchStore.acquireEditSession(type, globalId);
    if (session)
    {
      activeSession.value = session;

      // 如果编辑的是一个独立的模块，则默认选中它自己，
      // 以便在右侧的 EditorTargetRenderer 中立即显示其配置表单。
      if (session.type === 'module')
      {
        const moduleData = session.getData().value as AbstractModuleConfig | null;
        if (moduleData)
        {
          selectedConfig.value = {data: moduleData};
        }
      }
      // --- 修复逻辑结束 ---

      // 辅助性UI逻辑：如果编辑的不是模块，自动打开“当前编辑”面板
      if (type !== 'module')
      {
        isEditorPanelVisible.value = true;
      }
    }
    else
    {
      message.error(`无法开始编辑 “${globalId}”。资源可能不存在或已损坏。`);
    }
  } catch (error)
  {
    console.error('获取编辑会话时发生意外错误:', error);
    message.error('开始编辑时发生未知错误。');
    activeSession.value = null;
  } finally
  {
    isAcquiringSession.value = false;
  }
}

/**
 * 浏览器关闭前的警告，防止用户意外丢失未保存的草稿。
 * @param {BeforeUnloadEvent} event
 */
const beforeUnloadHandler = (event: BeforeUnloadEvent) =>
{
  if (workbenchStore.hasDirtyDrafts)
  {
    event.preventDefault();
  }
};

onMounted(() =>
{
  window.addEventListener('beforeunload', beforeUnloadHandler);
  workbenchStore.moduleSchemasAsync.execute();
});

onBeforeUnmount(() =>
{
  window.removeEventListener('beforeunload', beforeUnloadHandler);
});
</script>

<style scoped>
.workbench-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

/* 确保编辑器布局占据剩余空间 */
.editor-layout {
  flex: 1;
  min-height: 0; /* 修复flex布局中的高度计算问题 */
}

.workbench-header {
  padding: 10px 24px;
  border-bottom: 1px solid #e8e8e8;
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-shrink: 0;
}

.header-left-group {
  display: flex;
  align-items: center;
  gap: 16px;
}

.header-right-controls {
  display: flex;
  gap: 16px;
  align-items: center;
}
</style>