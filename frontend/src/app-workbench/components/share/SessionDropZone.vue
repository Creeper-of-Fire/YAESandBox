<!-- START OF NEW FILE: src/app-workbench/components/shared/SessionDropZone.vue -->
<template>
  <div class="session-drop-zone-wrapper">
    <!-- 插槽，用于显示底层内容，比如 WorkbenchSidebar -->
    <slot v-if="session"></slot>

    <!-- 统一的拖拽层，它总是存在 -->
    <draggable
        v-model="dropZone"
        class="drop-zone"
        :class="{ 'empty-state': !session, 'active-overlay': isActive && session }"
        :group="{ name: 'main-drop-zone', put: ['workflows-group', 'steps-group', 'modules-group'] }"
        @add="handleDrop"
        @start="handleDragStart"
        @end="handleDragEnd"
    >
      <!-- 根据状态显示不同的内容 -->
      <!-- 1. 空状态下的提示 -->
      <n-empty v-if="!session" description="从左侧拖拽一个配置项到此处开始编辑" class="empty-prompt"/>

      <!-- 2. 激活会话时，拖拽过程中的遮罩提示 -->
      <div v-if="session && isActive" class="overlay-content">
        <n-icon size="48" :component="SwapHorizIcon"/>
        <p class="overlay-text">{{ overlayText }}</p>
      </div>
    </draggable>
  </div>
</template>

<script setup lang="ts">
import {computed, ref} from 'vue';
import {NEmpty, NIcon} from 'naive-ui';
import {SwapHorizOutlined as SwapHorizIcon} from '@vicons/material';
import {VueDraggable as draggable} from 'vue-draggable-plus';
import type {SortableEvent} from 'sortablejs';
import {type ConfigType, type EditSession} from '@/app-workbench/services/EditSession';

const props = defineProps<{
  session: EditSession | null; // 接收会话对象或null
}>();

const emit = defineEmits<{
  (e: 'start-session', payload: { type: ConfigType; id: string }): void;
}>();

const dropZone = ref([]);
const isActive = ref(false); // 控制遮罩是否可见
const draggedItemType = ref<ConfigType | null>(null);

// 在任何拖拽开始时
function handleDragStart(event: SortableEvent) {
  const type = (event.item as HTMLElement).dataset.dragType as ConfigType | undefined;
  if (!type) return;

  // 只有当操作被允许时，才激活视觉效果
  if (canDrop(type)) {
    isActive.value = true;
    draggedItemType.value = type;
  }
}

// 拖拽结束时
function handleDragEnd() {
  isActive.value = false;
  draggedItemType.value = null;
}

// 放置时
function handleDrop(event: SortableEvent) {
  const item = event.item as HTMLElement;
  const type = item.dataset.dragType as ConfigType;
  const id = item.dataset.dragId as string;
  item.remove();

  // 仅在可以放置时才发出事件
  if (canDrop(type)) {
    emit('start-session', {type, id});
  }
}

// 判断是否允许放置
function canDrop(dragType: ConfigType): boolean {
  // 如果没有会话，总是允许放置
  if (!props.session) return true;

  // 如果有会话，则根据类型判断
  switch (props.session.type) {
    case 'workflow':
      return dragType === 'workflow';
    case 'step':
      return ['workflow', 'step'].includes(dragType);
    case 'module':
      return true;
  }
  return false;
}

// ... overlayText 和 getChineseTypeName 辅助函数保持不变 ...
const overlayText = computed(() => {
  if (!draggedItemType.value || !props.session) return '';
  switch (props.session.type) {
    case 'workflow':
      if (draggedItemType.value === 'workflow') return '拖拽到此以替换当前工作流';
      break;
    case 'step':
      if (draggedItemType.value === 'workflow') return '拖拽到此以编辑该工作流';
      if (draggedItemType.value === 'step') return '拖拽到此以替换当前步骤';
      break;
    case 'module':
      return `拖拽到此以开始编辑该${getChineseTypeName(draggedItemType.value)}`;
  }
  return '不支持的操作';
});

function getChineseTypeName(type: ConfigType): string {
  const names: Record<ConfigType, string> = {workflow: '工作流', step: '步骤', module: '模块'};
  return names[type];
}
</script>


<style scoped>
.session-drop-zone-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
}

.drop-zone {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  z-index: 10;
  transition: background-color 0.2s, border-color 0.2s;
}

/* 默认状态下不拦截鼠标，除非它是空状态 */
.drop-zone:not(.empty-state) {
  pointer-events: none;
}

/* 空状态下的样式 */
.drop-zone.empty-state {
  display: flex;
  justify-content: center;
  align-items: flex-start;
  border: 2px dashed #dcdfe6;
  border-radius: 4px;
  box-sizing: border-box;
}

.empty-prompt {
  margin-top: 20%;
  pointer-events: none; /* 让n-empty不干扰拖拽事件 */
}

/* 当空状态被拖拽悬停时的视觉反馈 */
.drop-zone.empty-state.sortable-drag {
  border-color: #2080f0;
  background-color: #f0f8ff;
}

/* 激活会话时，拖拽过程中的遮罩层样式 */
.drop-zone.active-overlay {
  pointer-events: all; /* 激活时拦截鼠标 */
}

.overlay-content {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  background-color: rgba(32, 128, 240, 0.85);
  color: white;
}

.overlay-text {
  margin-top: 16px;
  font-size: 16px;
  font-weight: 500;
}
</style>