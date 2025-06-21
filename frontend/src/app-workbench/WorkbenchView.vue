<!-- START OF FILE: src/app-workbench/WorkbenchView.vue -->
<template>
  <div class="workbench-view">
    <!-- 1. 顶部控制栏 (未来可以独立成组件) -->
    <div class="workbench-header">
      <div class="header-left-group">
        <n-h3 style="margin: 0;">工作流编辑器</n-h3>
        <!-- 将开关直接放在 h3 旁边 -->
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
      <span style="color: red; font-weight: bold;">Debug: {{ isEditorPanelVisible }}</span>

      <!-- 右侧的控制项（例如全局AI配置按钮） -->
      <div class="header-right-controls">
        <n-button @click="showAiConfigModal = true">全局AI配置</n-button>
      </div>
    </div>

    <!-- 2. 使用我们的新布局 -->
    <EditorLayout
        :is-global-panel-visible="isGlobalPanelVisible"
        :is-editor-panel-visible="isEditorPanelVisible"
    >
      <!-- 全局资源面板插槽 -->
      <template #global-panel>
        <GlobalResourcePanel @start-editing="handleStartEditing"/>
      </template>

      <!-- 当前编辑结构插槽 -->
      <template #editor-panel>
        <!-- 【核心修正】重新添加 wrapper div，并赋予它 100% 的高度 -->
        <div class="editor-panel-wrapper">
          <SessionDropZone
              :session="activeSession"
              @start-session="handleStartEditing"
          >
            <!-- 插槽内容保持不变 -->
            <WorkbenchSidebar
                v-if="activeSession"
                :session="activeSession"
                :selected-module-id="selectedModuleId"
                @update:selected-module-id="selectedModuleId = $event"
            />
          </SessionDropZone>
        </div>
      </template>

      <!-- 主内容区插槽 -->
      <template #main-content>
        <n-spin :show="isAcquiringSession" description="正在加载编辑器...">
          <div v-if="activeSession" class="main-content-wrapper">
            <!-- 传递 session 和 selectedModuleId TODO 改为更智能的方式-->
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
import {NEmpty, NH3, NSpin, useMessage} from 'naive-ui';
import {useWorkbenchStore} from '@/app-workbench/stores/workbenchStore.ts';
import {type ConfigType, type EditSession} from '@/app-workbench/services/EditSession.ts';
import {VueDraggable as draggable} from "vue-draggable-plus";
import type {SortableEvent} from "sortablejs";
import EditorLayout from '@/app-workbench/layouts/EditorLayout.vue';
import GlobalResourcePanel from '@/app-workbench/components/panel/GlobalResourcePanel.vue';
import WorkbenchSidebar from '@/app-workbench/components/panel/WorkbenchSidebar.vue';
import EditorTargetRenderer from '@/app-workbench/components/module/EditorTargetRenderer.vue';
import AiConfigEditorPanel from "@/app-workbench/features/ai-config-panel/AiConfigEditorPanel.vue";
import SessionDropZone from "@/app-workbench/components/share/SessionDropZone.vue";

defineOptions({
  name: 'WorkbenchView'
});


const workbenchStore = useWorkbenchStore();
const message = useMessage();

// --- 状态管理 ---
const activeSession = ref<EditSession | null>(null);
const isAcquiringSession = ref(false);

const isGlobalPanelVisible = ref(true);
const isEditorPanelVisible = ref(true);

// --- 临时：控制模态框显示的状态 ---
const showAiConfigModal = ref(false);
// --- 临时：选择的模块ID ---
const selectedModuleId = ref<string | null>(null);

// --- 事件处理 ---

onMounted(() => {
  window.addEventListener('beforeunload', beforeUnloadHandler);
});

onBeforeUnmount(() => {
  window.removeEventListener('beforeunload', beforeUnloadHandler);
});

// 浏览器关闭前的警告
const beforeUnloadHandler = (event: BeforeUnloadEvent) => {
  // 检查 store 中是否有任何未提交的更改
  if (workbenchStore.hasDirtyDrafts) {
    // 标准做法是阻止默认行为，浏览器会显示一个通用的确认对话框。
    event.preventDefault();
  }
};

async function handleStartEditing({type, id: globalId}: { type: ConfigType; id: string }) {
  if (isAcquiringSession.value) return;

  if (activeSession.value && activeSession.value.globalId === globalId) {
    // 如果会话已激活，并且用户点击的是同一个会话，我们仍然可以根据类型调整编辑面板的可见性，以防用户手动关闭了它。
    if (type !== 'module') {
      isEditorPanelVisible.value = true; // 编辑工作流或步骤时，显示左侧结构面板
    }
    return;
  }

  selectedModuleId.value = null;

  isAcquiringSession.value = true;
  activeSession.value = null;

  try {
    const session = await workbenchStore.acquireEditSession(type, globalId);
    if (session) {
      activeSession.value = session;
      // 根据会话类型控制 isEditorPanelVisible
      if (type !== 'module') {
        isEditorPanelVisible.value = true; // 编辑工作流或步骤时，显示左侧结构面板
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
</script>

<style scoped>
.workbench-view {
  display: flex;
  flex-direction: column;
  height: 100vh;
}

.workbench-header {
  padding: 10px 24px;
  border-bottom: 1px solid #e8e8e8;
  display: flex;
  /* 确保左右两边均匀分布 */
  justify-content: space-between;
  align-items: center;
  flex-shrink: 0;
}

.editor-panel-wrapper,
.main-content-wrapper {
  height: 100%;
}

/* 左侧组合，包含标题和开关 */
.header-left-group {
  display: flex;
  align-items: center; /* 垂直居中对齐 */
  gap: 16px; /* 标题和开关之间的间距 */
}

/* 右侧控制项，保持靠右 */
.header-right-controls {
  display: flex;
  gap: 16px;
  align-items: center;
}
</style>
<!-- END OF FILE -->