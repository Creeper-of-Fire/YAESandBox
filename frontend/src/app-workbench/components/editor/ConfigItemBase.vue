<!-- src/app-workbench/components/ConfigItemBase.vue -->
<template>
  <div
      class="config-item-base"
      :class="{ 'is-selected': isSelected }"
      @click="$emit('click')"
      @dblclick="$emit('dblclick')"
  >
    <!-- 拖拽把手 -->
    <n-icon v-if="isDraggable" class="drag-handle" size="16">
      <DragHandleOutlined/>
    </n-icon>
    <!-- 条目名称 -->
    <span class="item-name">{{ name }}</span>
    <div class="item-actions">
      <!-- 动作插槽：可用于放置编辑按钮、删除按钮、更多操作菜单等 -->
      <slot name="actions"></slot>
    </div>
  </div>
  <!-- 内容插槽：用于在条目下方展开额外内容，例如步骤的子模块列表 -->
  <slot name="content-below"></slot>
</template>

<script setup lang="ts">
import { NIcon } from 'naive-ui';
import { DragHandleOutlined } from '@vicons/material';

// 定义组件的 props
defineProps<{
  name: string; // 显示的名称
  isSelected: boolean; // 是否处于选中状态
  isDraggable?: boolean; // 是否可拖拽（显示拖拽把手）
}>();

// 定义组件触发的事件
defineEmits(['click', 'dblclick']);
</script>

<style scoped>
/* 基础样式，所有可配置项的通用外观 */
.config-item-base {
  display: flex;
  align-items: center;
  padding: 6px 8px;
  background-color: #fff;
  border-radius: 4px;
  border: 1px solid #e0e0e6;
  cursor: pointer;
  position: relative; /* 用于内部元素定位 */
}

/* 选中状态的样式 */
.config-item-base.is-selected {
  border-color: #2080f0; /* Naive UI 主题色 */
  box-shadow: 0 0 0 1px #2080f0; /* 选中时的蓝色边框效果 */
}

/* 鼠标悬停样式 */
.config-item-base:hover {
  background-color: #f7f9fa; /* 浅灰色背景 */
}

/* 拖拽把手样式 */
.drag-handle {
  cursor: grab; /* 抓取光标 */
  color: #aaa; /* 灰色 */
  margin-right: 8px;
  flex-shrink: 0; /* 防止把手被挤压 */
}

.drag-handle:active {
  cursor: grabbing; /* 拖拽时光标变化 */
}

/* 名称文本样式 */
.item-name {
  flex-grow: 1; /* 占据剩余空间 */
  overflow: hidden; /* 隐藏溢出内容 */
  text-overflow: ellipsis; /* 溢出时显示省略号 */
  white-space: nowrap; /* 不换行 */
}

/* 动作区域样式 */
.item-actions {
  margin-left: 8px;
  flex-shrink: 0; /* 防止动作按钮被挤压 */
}
</style>