<!-- 文件路径: src/app-workbench/views/WorkbenchView.vue -->
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
        <div v-if="activeSession" class="editor-panel-wrapper">
          <WorkbenchSidebar
              :session="activeSession"
              :selected-module-id="selectedModuleId"
              @update:selected-module-id="selectedModuleId = $event"
          />
        </div>
        <!-- 彻底替换为原生 div 和原生事件监听 -->
        <div v-else
            class="main-drop-zone"
            :class="{ 'is-dragging-over': isDraggingOver }"
            @dragover.prevent="handleDragOver"
            @dragleave.prevent="handleDragLeave"
            @drop.prevent="handleDrop"
        >
          <!-- 这个 div 内部只放视觉提示，不再有任何拖拽库 -->
          <div class="drop-zone-background">
            <n-empty description="从左侧拖拽一个配置项到此处开始编辑" style="margin-top: 20%;"/>
          </div>
        </div>
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
import {VueDraggable as draggable} from "vue-draggable-plus";
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
const isAcquiringSession = ref(false);

// --- UI 控制状态 ---
const isGlobalPanelVisible = ref(true);
const isEditorPanelVisible = ref(true);
const selectedModuleId = ref<string | null>(null);
const showAiConfigModal = ref(false);

// 为主内容区的拖拽接收区提供一个 v-model (即使它永远是空的)
// 新增一个 ref 来控制拖拽悬浮时的样式
const isDraggingOver = ref(false);

/**
 * 处理原生拖拽悬浮事件
 * @param event 原生的 DragEvent
 */
function handleDragOver(event: DragEvent) {
  // 关键：必须在这里调用 preventDefault() 来告诉浏览器这是一个有效的放置目标。
  // console.log('handleDragOver', event)
  event.preventDefault();

  isDraggingOver.value = true;
  if (event.dataTransfer) {
    // 设置放置效果为“复制”，会显示一个带加号的鼠标指针
    event.dataTransfer.dropEffect = 'copy';
  }
}

/**
 * 处理原生拖拽离开事件
 */
function handleDragLeave() {
  console.log('handleDragLeave');
  isDraggingOver.value = false;
}

/**
 * 处理原生放置事件
 * @param event 原生的 DragEvent
 */
function handleDrop(event: DragEvent) {
  event.preventDefault();
  console.log('handleDrop', event);
  isDraggingOver.value = false; // 重置悬浮样式
  if (event.dataTransfer) {
    try {
      // 从 dataTransfer 中取出我们之前存入的JSON字符串
      const dataString = event.dataTransfer.getData('text/plain');
      if (dataString) {
        const { type, id } = JSON.parse(dataString) as { type: ConfigType; id: string };
        if (type && id) {
          handleStartEditing({ type, id });
        }
      }
    } catch (e) {
      console.error("解析拖拽数据失败:", e);
    }
  }
}

/**
 * 处理从左侧面板点击“编辑”按钮或双击列表项的事件。
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

/* 覆盖 N-Switch 的 CSS 变量以实现自定义颜色 */
:deep(.n-switch.n-switch--active) {
  --n-rail-color: #18a058 !important;
  --n-rail-color-hover: #18a058 !important;
}

:deep(.n-switch:not(.n-switch--active)) {
  --n-rail-color: #a3a3a3 !important;
  --n-rail-color-hover: #a3a3a3 !important;
}

:deep(.n-switch__content) {
  color: white;
}

.n-spin-container,
:deep(.n-spin-content) {
  height: 100%;
}

.editor-panel-wrapper,
.main-content-wrapper {
  height: 100%;
}

/* 移除对绝对定位和z-index的依赖，简化布局 */
.main-drop-zone {
  width: 100%;
  height: 100%;
  border: 2px dashed #dcdfe6;
  border-radius: 8px;
  box-sizing: border-box;
  transition: border-color 0.2s, background-color 0.2s;
  position: relative; /* 为内部的 n-empty 定位提供基准 */
}

/* 使用新的 is-dragging-over class 来控制悬浮样式 */
.main-drop-zone.is-dragging-over {
  border-color: #2080f0;
  background-color: rgba(32, 128, 240, 0.05);
}

.drop-zone-background {
  width: 100%;
  height: 100%;
  display: flex;
  justify-content: center;
  align-items: flex-start;
  pointer-events: none; /* 确保不干扰拖拽事件 */
}

</style>