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
            strong secondary type="primary"
            :disabled="!workbenchStore.hasDirtyDrafts"
            :loading="isSavingAll"
            @click="handleSaveAll"
        >
          <template #icon>
            <n-icon :component="SaveIcon"/>
          </template>
          全部保存
        </n-button>
        <n-button @click="showAiConfigModal = true">全局AI配置</n-button>
      </n-space>

      <!-- 右侧：其他全局操作 -->
      <div class="header-right-controls">
        <n-button @click="showAiConfigModal = true">全局AI配置</n-button>
      </div>
    </div>

    <!-- 2. 编辑器核心布局 -->
    <EditorLayout
        :is-global-panel-visible="isGlobalPanelVisible"
        :is-editor-panel-visible="isEditorPanelVisible"
    >
      <!-- 2a. 全局资源面板插槽 -->
      <template #global-panel>
        <GlobalResourcePanel @start-editing="handleStartEditing"/>
      </template>

      <!-- 2b. 当前编辑结构插槽 (侧边栏) -->
      <template #editor-panel>
        <WorkbenchSidebar
            :key="activeSession?.globalId ?? 'empty-session'"
            :session="activeSession"
            :selected-module-id="selectedModuleId"
            @update:selected-module-id="selectedModuleId = $event"
            @start-editing="handleStartEditing"
            @closeSession="handleCloseSession"
        />
      </template>

      <!-- 2c. 主内容区插槽 -->
      <template #main-content>
        <n-spin :show="isAcquiringSession" description="正在加载编辑器...">
          <!-- 如果有激活的会话，显示模块编辑器 -->
          <div v-if="activeSession" class="main-content-wrapper">
            <EditorTargetRenderer
                :session="activeSession"
                :selected-module-id="selectedModuleId"
            />
          </div>
          <n-empty v-else description="无激活的编辑会话" style="margin-top: 20%;"/>
        </n-spin>
      </template>

    </EditorLayout>

    <!-- 3. 临时：承载 AI 配置面板的模态框 -->
    <n-modal
        v-model:show="showAiConfigModal"
        title="全局 AI 配置中心"
        preset="card"
        style="width: 90%; max-width: 1400px; height: 90vh;"
        :mask-closable="false"
        :closable="true"
        :bordered="false"
    >
      <div style="height: calc(90vh - 100px); overflow-y: auto;">
        <AiConfigEditorPanel/>
      </div>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import {onBeforeUnmount, onMounted, ref} from 'vue';
import {NButton, NEmpty, NH3, NSpin, NSwitch, useMessage} from 'naive-ui';
import {SaveOutlined as SaveIcon} from '@vicons/material';
import type {SortableEvent} from "sortablejs";
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore';
import {type ConfigType, type EditSession} from '@/app-workbench/services/EditSession';

import EditorLayout from '@/app-workbench/layouts/EditorLayout.vue';
import GlobalResourcePanel from '@/app-workbench/components/panel/GlobalResourcePanel.vue';
import WorkbenchSidebar from '@/app-workbench/components/panel/WorkbenchSidebar.vue';
import EditorTargetRenderer from '@/app-workbench/components/module/EditorTargetRenderer.vue';
import AiConfigEditorPanel from "@/app-workbench/features/ai-config-panel/AiConfigEditorPanel.vue";

defineOptions({
  name: 'WorkbenchView'
});

// TODO 使用vueuse的useStore来存储单例的UI状态
// TODO 实现拖拽的自动升级/降级（比如把工作流拖入工作流就意味着替换当前编辑对象，把工作流/步骤拖入步骤同理）

const workbenchStore = useWorkbenchStore();
const message = useMessage();

// --- 核心状态 ---
const activeSession = ref<EditSession | null>(null);


// 用于“正在请求新编辑视图”的加载状态
const isAcquiringSession = ref(false);
// 用于“全部保存”按钮的加载状态
const isSavingAll = ref(false);

// --- UI 控制状态 ---
const isGlobalPanelVisible = ref(true);
const isEditorPanelVisible = ref(true);
const selectedModuleId = ref<string | null>(null);
const showAiConfigModal = ref(false);

/**
 * *** 处理全局保存操作 ***
 */
async function handleSaveAll() {
  isSavingAll.value = true;
  try {
    await workbenchStore.saveAllDirtyDrafts();
    message.success("所有更改已成功保存！");
  } catch (error) {
    message.error("保存时发生错误，请查看控制台。");
    console.error("全部保存失败:", error);
  } finally {
    isSavingAll.value = false;
  }
}

/**
 * *** 处理关闭会话的请求 ***
 */
function handleCloseSession() {
  activeSession.value = null;
}

/**
 * 处理从左侧面板点击“编辑”按钮或双击列表项的事件。或者其他的“开始编辑”事件
 * @param {object} payload - 包含类型和ID的对象。
 */
async function handleStartEditing({type, id: globalId}: { type: ConfigType; id: string }) {
  if (isAcquiringSession.value) return;

  // 如果点击的是当前已激活的会话，则根据类型辅助性地打开面板，然后直接返回
  if (activeSession.value && activeSession.value.globalId === globalId) {
    if (type !== 'module') {
      isEditorPanelVisible.value = true;
    }
    return;
  }

  // 切换新会话前，重置UI状态
  selectedModuleId.value = null;
  isAcquiringSession.value = true;
  activeSession.value = null; // 先清空，让UI显示加载状态

  try {
    const session = await workbenchStore.acquireEditSession(type, globalId);
    if (session) {
      activeSession.value = session;
      // 辅助性UI逻辑：如果编辑的不是模块，自动打开“当前编辑”面板
      if (type !== 'module') {
        isEditorPanelVisible.value = true;
      }
    } else {
      message.error(`无法开始编辑 “${globalId}”。资源可能不存在或已损坏。`);
    }
  } catch (error) {
    console.error('获取编辑会话时发生意外错误:', error);
    message.error('开始编辑时发生未知错误。');
    activeSession.value = null;
  } finally {
    isAcquiringSession.value = false;
  }
}

/**
 * 处理从左侧面板拖拽资源到主内容区“空状态”的事件。
 * @param {SortableEvent} event - SortableJS 的 'add' 事件对象
 */
function handleDropInMainArea(event: SortableEvent) {
  // event.item 是被拖过来的那个 div 元素的引用
  const item = event.item as HTMLElement;

  // 从我们之前在 GlobalResourcePanel 中设置的 data-* 属性中获取类型和ID
  const type = item.dataset.dragType as ConfigType | undefined;
  const id = item.dataset.dragId;

  // 关键：立即从DOM中移除被拖拽进来的临时元素，因为主区域只是个“触发器”
  item.remove();

  if (type && id) {
    // 调用我们已有的函数来开始编辑会话
    handleStartEditing({type, id});
  } else {
    // 如果没有找到必要信息，发出警告
    console.warn("拖拽项缺少 'data-drag-type' 或 'data-drag-id' 属性。");
  }
}

/**
 * 浏览器关闭前的警告，防止用户意外丢失未保存的草稿。
 * @param {BeforeUnloadEvent} event
 */
const beforeUnloadHandler = (event: BeforeUnloadEvent) => {
  if (workbenchStore.hasDirtyDrafts) {
    event.preventDefault();
  }
};

onMounted(() => {
  window.addEventListener('beforeunload', beforeUnloadHandler);
});

onBeforeUnmount(() => {
  window.removeEventListener('beforeunload', beforeUnloadHandler);
});
</script>

<style scoped>
.workbench-view {
  display: flex;
  flex-direction: column;
  height: 100%;
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