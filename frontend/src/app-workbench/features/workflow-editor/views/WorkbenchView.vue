<!-- START OF FILE: src/app-workbench/views/WorkbenchView.vue -->
<template>
  <div class="workbench-view">
    <!-- 1. 顶部控制栏 (未来可以独立成组件) -->
    <div class="workbench-header">
      <n-h3 style="margin: 0;">工作流编辑器</n-h3>
      <div class="header-controls">
        <!-- 控制侧边栏可见性的开关 -->
        <n-checkbox v-model:checked="isGlobalPanelVisible">全局资源</n-checkbox>
        <n-checkbox v-model:checked="isEditorPanelVisible">当前编辑</n-checkbox>

        <!-- 临时：打开 AI 配置的按钮 -->
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
        <GlobalResourcePanel @start-editing="handleStartEditing" />
      </template>

      <!-- 当前编辑结构插槽 -->
      <template #editor-panel>
        <div v-if="activeSession" class="editor-panel-wrapper">
          <WorkbenchSidebar :session="activeSession" />
        </div>
        <n-empty v-else description="请从左侧选择或拖拽一个配置项开始编辑" />
      </template>

      <!-- 主内容区插槽 -->
      <template #main-content>
        <n-spin :show="isAcquiringSession" description="正在加载编辑器...">
          <div v-if="activeSession" class="main-content-wrapper">
            <EditorTargetRenderer :session="activeSession" />
          </div>
          <n-empty v-else description="无激活的编辑会话" style="margin-top: 20%;" />
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
        <AiConfigEditorPanel />
      </div>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import {ref} from 'vue';
import {NCheckbox, NEmpty, NH3, NSpin, useMessage} from 'naive-ui';
import {useWorkbenchStore} from '@/app-workbench/features/workflow-editor/stores/workbenchStore.ts';
import {type ConfigType, type EditSession} from '@/app-workbench/features/workflow-editor/services/EditSession.ts';

import EditorLayout from '@/app-workbench/features/workflow-editor/layouts/EditorLayout.vue';
import GlobalResourcePanel from '@/app-workbench/features/workflow-editor/components/GlobalResourcePanel.vue';
import WorkbenchSidebar from '@/app-workbench/features/workflow-editor/components/WorkbenchSidebar.vue';
import EditorTargetRenderer from '@/app-workbench/features/workflow-editor/components/EditorTargetRenderer.vue';
import AiConfigEditorPanel from "@/app-workbench/features/ai-config-panel/AiConfigEditorPanel.vue";

const workbenchStore = useWorkbenchStore();
const message = useMessage();

// --- 状态管理 ---
const activeSession = ref<EditSession | null>(null);
const isAcquiringSession = ref(false);
const isGlobalPanelVisible = ref(true);
const isEditorPanelVisible = ref(true);

// --- 临时：控制模态框显示的状态 ---
const showAiConfigModal = ref(false);

// --- 事件处理 ---
async function handleStartEditing({ type, id: globalId }: { type: ConfigType; id: string }) {
  if (isAcquiringSession.value) return;

  if (activeSession.value) {
    if (activeSession.value.globalId === globalId && activeSession.value.type === type) return;
    if (!activeSession.value.close()) return;
  }

  isAcquiringSession.value = true;
  activeSession.value = null;

  try {
    activeSession.value = await workbenchStore.acquireEditSession(type, globalId);
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
  justify-content: space-between;
  align-items: center;
  flex-shrink: 0;
}
.header-controls {
  display: flex;
  gap: 16px;
}
.editor-panel-wrapper,
.main-content-wrapper {
  height: 100%;
}
</style>
<!-- END OF FILE -->