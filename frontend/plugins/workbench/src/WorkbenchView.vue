<!-- 文件路径: src/app-workbench/WorkbenchView.vue -->
<template>
  <div class="workbench-view">
    <EditorLayout
        class="editor-layout"
    >
      <template #header-left-group>
        <!-- 左侧：标题和紧邻的开关 -->
        <div class="header-left-group">
          <n-h3 style="margin: 0;">工作流编辑器</n-h3>
        </div>

      </template>
      <template #header-right-group>
        <!-- 右侧的全局操作按钮 -->
        <n-flex class="header-right-controls">
          <n-button
              :disabled="!allDirty" :loading="isSavingAll" secondary
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
        </n-flex>
      </template>

      <!-- 全局资源面板插槽 -->
      <template #global-panel>
        <GlobalResourcePanel @start-editing="handleStartEditing"/>
      </template>

      <!-- 当前编辑结构插槽 -->
      <template #editor-panel>
        <WorkbenchSidebar
            :key="activeContext?.storeId ?? 'empty-session'"
            @closeSession="handleCloseSession"
            @start-editing="handleStartEditing"
        />
      </template>

      <!-- 主内容区插槽 -->
      <template #rune-panel>
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
import {computed, onBeforeUnmount, onMounted, ref} from 'vue';
import {NButton, NH3, useDialog, useMessage, useThemeVars} from 'naive-ui';
import {SaveIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {useWorkbenchStore} from '#/stores/workbenchStore';
import {type ConfigType} from '#/services/GlobalEditSession.ts';

import EditorLayout from '#/layouts/EditorLayout.vue';
import GlobalResourcePanel from '#/components/panel/GlobalResourcePanel.vue';
import WorkbenchSidebar from '#/components/panel/WorkbenchSidebar.vue';
import AiConfigEditorPanel from "#/features/ai-config-panel/AiConfigEditorPanel.vue";
import MainEditPanel from "#/components/panel/MainEditPanel.vue";

//@ts-ignore
import '@vue-flow/core/dist/style.css';
//@ts-ignore
import '@vue-flow/core/dist/theme-default.css';

// @ts-ignore
import '@vue-flow/controls/dist/style.css';

import {createActiveEditorContextProvider} from "#/services/editor-context/useSelectedConfig.ts";

defineOptions({
  name: 'WorkbenchView'
});

// --- 使用 Composable ---
const workbenchStore = useWorkbenchStore();
const {activeContext, switchContext, closeContext, isLoading} = createActiveEditorContextProvider();


// TODO 使用vueuse的useStore来存储单例的UI状态

const message = useMessage();
const dialog = useDialog();

// --- 核心状态 ---
// 用于“全部保存”按钮的加载状态
const isSavingAll = ref(false);

// --- UI 控制状态 ---
const showAiConfigModal = ref(false);


/**
 * 处理“开始编辑”事件。
 * 直接调用 Composable 中的方法。
 */
async function handleStartEditing({type, storeId}: { type: ConfigType; storeId: string })
{
  await switchContext(type, storeId);
}

/**
 * 处理关闭会话的请求。
 * 直接调用 Composable 中的方法。
 */
function handleCloseSession()
{
  closeContext();
}

const allDirty = computed(() =>
{
  return workbenchStore.hasDirtyDrafts;
});

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
      return item.name || item.storeId;
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
 * 浏览器关闭前的警告，防止用户意外丢失未保存的草稿。
 * @param {BeforeUnloadEvent} event
 */
const beforeUnloadHandler = (event: BeforeUnloadEvent) =>
{
  if (allDirty.value)
  {
    event.preventDefault();
  }
};

onMounted(async () =>
{
  window.addEventListener('beforeunload', beforeUnloadHandler);
  await workbenchStore.runeSchemasAsync.execute();
});

onBeforeUnmount(() =>
{
  window.removeEventListener('beforeunload', beforeUnloadHandler);
});

const themeVars = useThemeVars();
</script>

<style scoped>
.workbench-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  box-sizing: border-box;
}

/* 确保编辑器布局占据剩余空间 */
.editor-layout {
  flex: 1;
  min-height: 0; /* 修复flex布局中的高度计算问题 */
}
</style>