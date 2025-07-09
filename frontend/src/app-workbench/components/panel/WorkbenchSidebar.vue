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
          <span class="title-text">编辑{{ currentConfigName }}</span>
          <n-popover trigger="hover">
            <template #trigger>
              <n-button style="font-size: 20px;" text @click="handleClose">
                <n-icon :component="CloseIcon"/>
              </n-button>
            </template>
            关闭编辑视图
          </n-popover>
        </n-h4>


        <n-space class="action-bar" justify="space-between">
          <n-space>
            <n-button secondary size="small" strong type="primary" @click="handleRename">重命名</n-button>
          </n-space>
          <n-space>
            <n-button :disabled="!isDirty" secondary size="small" strong type="error" @click="handleDiscard">放弃</n-button>
            <n-button :disabled="!isDirty" secondary size="small" strong type="success" @click="handleSave">保存</n-button>
          </n-space>
        </n-space>
        <!-- 分割线，让布局更清晰 -->
        <n-divider style="margin-top: 12px; margin-bottom: 12px;"/>
      </template>


      <!-- 2b. 可滚动的内容区域 -->
      <template #body>
        <template v-if="session.type === 'workflow' && workflowData">
          <p class="sidebar-description">拖拽全局步骤到步骤列表，或将全局资源拖到此区域的任意位置以替换当前编辑项。</p>
          <WorkflowItemRenderer :workflow="workflowData"/>
        </template>

        <template v-else-if="session.type === 'step' && stepData">
          <p class="sidebar-description">拖拽全局模块到模块列表，或将全局资源拖到此区域的任意位置以替换当前编辑项。</p>
          <StepItemRenderer
              :is-collapsible="false"
              :is-draggable="false"
              :step="stepData"
              style="margin-top: 16px"
          />
        </template>

        <template v-else-if="session.type === 'module' && moduleData">
          <n-alert style="margin-top: 16px;" title="提示" type="info">
            这是一个独立的模块。请在中间的主编辑区完成详细配置。
          </n-alert>
        </template>
      </template>
    </HeaderAndBodyLayout>
    <div v-else class="empty-state-wrapper">
      <div class="custom-empty-state">
        <n-icon :component="AddBoxIcon" :size="80"/>
        <p class="description">从左侧拖拽一项到此处开始编辑</p>
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
import {computed, h, ref} from 'vue';
import {NH4, NIcon, NInput, useDialog, useMessage} from 'naive-ui';
import type {ConfigType, EditSession} from "@/app-workbench/services/EditSession.ts";
import type {
  AbstractModuleConfig,
  StepProcessorConfig,
  WorkflowProcessorConfig
} from "@/app-workbench/types/generated/workflow-config-api-client";
import StepItemRenderer from '../step/StepItemRenderer.vue';
import WorkflowItemRenderer from "@/app-workbench/components/workflow/WorkflowItemRenderer.vue";
import {AddBoxIcon, SwapHorizIcon} from '@/utils/icons';
import {CloseIcon} from "naive-ui/es/_internal/icons";
import HeaderAndBodyLayout from "@/app-workbench/layouts/HeaderAndBodyLayout.vue";
import ModuleItemRenderer from "@/app-workbench/components/module/ModuleItemRenderer.vue";

const props = defineProps<{
  session: EditSession | null;
}>();

const emit = defineEmits<{
  (e: 'start-editing', payload: { type: ConfigType; id: string }): void;
  (e: 'close-session'): void;
}>();

const dialog = useDialog();
const message = useMessage();

// --- 状态 ---
// 覆盖层的显隐状态
const isDragOverContainer = ref(false);

// --- 计算属性 ---
// 通过 session 获取 isDirty 状态
const isDirty = computed(() => props.session?.getIsDirty().value ?? false);

// --- 按钮事件处理 ---

async function handleSave()
{
  if (!props.session) return;

  const result = await props.session.save();
  if (result.success)
  {
    message.success(`“${result.name}” 已保存!`);
  }
  else
  {
    const error = result.error;
    // 捕获 EditSession.save 抛出的错误
    message.error(`保存失败：${result.name}，请检查内容或查看控制台。`);
    console.error(`${result.name} 保存失败，详情:`, error);
  }

}

function handleDiscard()
{
  if (!props.session) return;
  dialog.warning({
    title: '放弃更改',
    content: '您确定要放弃所有未保存的更改吗？此操作不可撤销。',
    positiveText: '确定放弃',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      props.session?.discard();
      message.info("更改已放弃。");
      // 放弃后，数据会恢复原状，但会话依然存在。
    },
  });
}

function handleClose()
{
  emit('close-session');
}

function handleRename()
{
  if (!props.session) return;
  const currentName = ref(props.session.getData().value?.name ?? '');

  dialog.create({
    title: '重命名',
    content: () => h(NInput, {
      value: currentName.value,
      onUpdateValue: (v) =>
      {
        currentName.value = v;
      },
      placeholder: '请输入新的名称',
    }),
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      if (!currentName.value.trim())
      {
        message.error("名称不能为空！");
        return false; // 阻止对话框关闭
      }
      props.session?.rename(currentName.value);
    }
  });
}

// --- 等级定义和比较逻辑 ---

// 定义配置类型的等级
const typeHierarchy: Record<ConfigType, number> = {
  workflow: 3,
  step: 2,
  module: 1,
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
    const match = type.match(/^application\/vnd\.workbench\.item\.(workflow|step|module)$/);
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
  if (!props.session)
  {
    isDragOverContainer.value = true;
    return;
  }

  // 获取当前会话和拖拽物的等级
  const currentSessionType = props.session.type;
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
    props.session?.type === 'workflow' ? props.session.getData().value as WorkflowProcessorConfig : null
);
const stepData = computed(() =>
    props.session?.type === 'step' ? props.session.getData().value as StepProcessorConfig : null
);
const moduleData = computed(() =>
    props.session?.type === 'module' ? props.session.getData().value as AbstractModuleConfig : null
);
const currentConfigName = computed(() =>
{
  if (!props.session) return ''; // 如果没有会话，返回空字符串
  if (props.session.type === 'workflow' && workflowData.value) return `工作流: ${workflowData.value.name}`;
  if (props.session.type === 'step' && stepData.value) return `步骤: ${stepData.value.name}`;
  if (props.session.type === 'module' && moduleData.value) return `模块: ${moduleData.value.name}`;
  return '未知';
});
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
  border: 2px dashed #dcdfe6;
  border-radius: 8px;
  box-sizing: border-box;
  background-color: #fafafc;
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
  color: #c0c4cc; /* 图标的颜色，比较柔和 */
  text-align: center;
  pointer-events: none; /* 防止它干扰拖拽事件 */
}

.custom-empty-state .description {
  font-size: 16px; /* 加大字体 */
  font-weight: 500;
  color: #a8abb2; /* 文字的颜色 */
  max-width: 220px; /* 控制文字宽度，使其在必要时换行 */
  line-height: 1.5;
}

.sidebar-description {
  color: #888;
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
  background-color: rgba(32, 128, 240, 0.15);
  border: 2px dashed #2080f0;
  border-radius: 6px;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: center;
  box-sizing: border-box;
}

.drop-overlay-content {
  text-align: center;
  color: #2080f0;
  font-weight: 500;
  pointer-events: none;
}
</style>