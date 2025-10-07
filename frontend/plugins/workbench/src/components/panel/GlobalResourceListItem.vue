<!-- src/app-workbench/components/.../GlobalResourceListItem.vue -->
<template>
  <div v-if="item && item.isSuccess"
       class="resource-item"
       :class="{ 'is-dirty': isDirty }"
       @click="handleStartEditing"
       @contextmenu.prevent="$emit('contextmenu', { type, storeId:storeId, name: item.data.name, event: $event })"
  >

    <div class="drag-handle-tree">
      <n-icon>
        <DragHandleIcon/>
      </n-icon>
    </div>

    <span class="item-name">
        {{ item.data.name }}
      <!-- 如果变脏，显示一个小星号 -->
        <span v-if="isDirty">*</span>
    </span>

    <!-- 将 text 按钮替换为更显眼的 secondary 图标按钮 -->
    <n-popover trigger="hover">
      <template #trigger>
        <n-button
            circle
            secondary
            size="small"
            strong
            type="primary"
            @click.stop="handleStartEditing"
        >
          <template #icon>
            <n-icon :component="EditIcon"/>
          </template>
        </n-button>
      </template>
      编辑 “{{ item.data.name }}”
    </n-popover>
  </div>
  <div v-else-if="item && !item.isSuccess"
       class="resource-item-damaged"
       @contextmenu.prevent="$emit('contextmenu', { type, storeId:storeId, name: storeId, isDamaged: true, event: $event })"
  >
    <n-icon :component="LinkOffIcon" color="#d03050"/>
    <n-popover trigger="hover">
      <template #trigger>
        <span class="damaged-text">{{ storeId }} (已损坏)</span>
      </template>
      {{ item.errorMessage }}
    </n-popover>
    <!-- 同样美化损坏项的“详情”按钮 -->
    <n-popover trigger="hover">
      <template #trigger>
        <n-button
            circle
            secondary
            size="small"
            strong
            type="error"
            @click.stop="$emit('show-error-detail', item.errorMessage, item.originJsonString)"
        >
          <template #icon>
            <n-icon :component="FindInPageIcon"/>
          </template>
        </n-button>
      </template>
      查看错误详情
    </n-popover>
  </div>
</template>

<script lang="ts" setup>
import {NButton, NIcon, useThemeVars} from 'naive-ui';
import {DragHandleIcon, EditIcon, FindInPageIcon, LinkOffIcon} from '@yaesandbox-frontend/shared-ui/icons';
import type {AnyConfigObject, ConfigType} from '#/services/GlobalEditSession.ts';
import type {GlobalResourceItem} from '@yaesandbox-frontend/core-services/types';
import {computed} from "vue";
import {useWorkbenchStore} from "#/stores/workbenchStore.ts";
import {useEditorControlPayload} from '#/services/editor-context/useSelectedConfig.ts';

const props = defineProps<{
  storeId: string; // 原始 Record 的 key (资源的唯一ID)
  type: ConfigType; // 资源的类型 ('workflow', 'tuum', 'rune')
}>();

const emit = defineEmits<{
  (e: 'show-error-detail', errorMessage: string, originJsonString: string | null | undefined): void; // 点击“详情”时触发
  (e: 'contextmenu', payload: {
    type: ConfigType;
    storeId: string;
    name: string;
    isDamaged?: boolean;
    event: MouseEvent | PointerEvent
  }): void;
}>();

// *** 检查当前项是否变脏 ***
const workbenchStore = useWorkbenchStore();

const isDirty = computed(() =>
{
  // 从 store 获取所有活跃的会话
  const sessions = workbenchStore.getActiveSessions;
  // 查找与当前列表项 storeId 匹配的会话
  const currentSession = sessions[props.storeId];
  // 如果找到了会话，则返回其 isDirty 状态；否则返回 false
  return currentSession ? currentSession.getIsDirty().value : false;
});

// 从 store 中动态获取 item 数据
const item = computed(() => {
  // 根据类型从对应的资源 map 中查找
  switch (props.type) {
    case 'workflow':
      return workbenchStore.globalWorkflowsAsync.state?.[props.storeId];
    case 'tuum':
      return workbenchStore.globalTuumsAsync.state?.[props.storeId];
    case 'rune':
      return workbenchStore.globalRunesAsync.state?.[props.storeId];
    default:
      return null;
  }
});


const {switchContext} = useEditorControlPayload();

function handleStartEditing()
{
  switchContext(props.type, props.storeId);
}

const themeVars = useThemeVars();
</script>

<style scoped>
/* 正常资源项的样式 */
.resource-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 8px; /* 增加一点垂直内边距以适应更大的按钮 */
  border-radius: 4px;
  cursor: pointer; /* 可点击编辑 */
}

.resource-item.is-dirty {
  background-color: v-bind('themeVars.warningColorSuppl');
}

.resource-item.is-dirty .item-name {
  font-weight: 500; /* 加粗一点点 */
}

.resource-item:hover {
  background-color: v-bind('themeVars.hoverColor'); /* 悬停背景色 */
}

.drag-handle-tree {
  padding: 0 8px;
  cursor: grab;
  color: v-bind('themeVars.textColor3');
}
.drag-handle-tree:active {
  cursor: grabbing;
}

/* 让名称和按钮之间的空间更大，避免误触 */
.item-name {
  flex-grow: 1;
  margin-right: 8px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.resource-item-damaged {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 8px; /* 同样增加垂直内边距 */
  border-radius: 4px;
  cursor: default; /* 损坏项不可直接编辑，光标为默认 */
  color: v-bind('themeVars.errorColor'); /* 错误红色 */
}

.damaged-text {
  margin-left: 8px;
  margin-right: 8px;
  flex-grow: 1; /* 占据剩余空间 */
  overflow: hidden; /* 隐藏溢出内容 */
  text-overflow: ellipsis; /* 溢出时显示省略号 */
  white-space: nowrap; /* 不换行 */
}

</style>
<!-- END OF MODIFIED FILE -->