<!-- src/app-workbench/components/.../ConfigItemBase.vue -->
<template>
  <div
      :class="{ 'is-selected': isSelected , 'is-disabled': !enabled }"
      class="config-item-base"
      @click="$emit('click')"
      @dblclick="$emit('dblclick')"
  >
    <!-- 拖拽把手 -->
    <div v-if="isDraggable"
         class="drag-handle"
         @click.stop
    >
      <n-icon :size="18">
        <DragHandleOutlined/>
      </n-icon>
    </div>

    <div class="enable-switch-wrapper" @click.stop>
      <n-switch
          :round="false"
          :value="enabled"
          size="small"
          @update:value="(newValue:boolean) => $emit('update:enabled', newValue)"
      >
      </n-switch>
    </div>

    <!-- 主内容区插槽 -->
    <div class="item-content-wrapper">
      <slot name="content"></slot>
    </div>

    <div class="item-actions">
      <!-- 动作插槽：可用于放置编辑按钮、删除按钮、更多操作菜单等 -->
      <slot name="actions"></slot>
    </div>
  </div>
  <!-- 内容插槽：用于在条目下方展开额外内容，例如祝祷的子符文列表 -->
  <slot name="content-below"></slot>
</template>

<script lang="ts" setup>
import {NIcon} from 'naive-ui';
import {DragHandleOutlined} from '@/utils/icons.ts';
import {computed} from "vue";
import ColorHash from "color-hash";

// 定义组件的 props
const props = defineProps<{
  isCollapsible?: boolean; // 是否可以展开下面的内容
  isSelected: boolean; // 是否处于选中状态
  isDraggable?: boolean; // 是否可拖拽（显示拖拽把手）
  highlightColorCalculator: string;
  enabled: boolean;
}>();

const colorHash = new ColorHash({
  lightness: [0.7, 0.75, 0.8],
  saturation: [0.7, 0.8, 0.9],
  hash: 'bkdr'
});
const highlightColor = computed(() =>
{
  return colorHash.hex(props.highlightColorCalculator);
});

// 定义组件触发的事件
defineEmits(['click', 'dblclick', 'update:enabled']);

/**
 * 计算属性，用于处理选中状态下的边框颜色。
 * 如果 highlightColor 存在，则使用它，否则回退到默认的蓝色主题色。
 */
const finalHighlightColor = computed(() => highlightColor.value || '#2080f0');

/**
 * 计算属性，用于拖拽区域的背景色
 * 如果没有提供 highlightColor，则使用一个柔和的灰色作为默认值
 */
const handleBgColor = computed(() => highlightColor.value ? `${highlightColor.value}33` : '#f7f9fa');
</script>

<style scoped>
/* 基础样式，所有可配置项的通用外观 */
.config-item-base {
  display: flex;
  align-items: stretch;
  padding: 0;
  background-color: #fff;
  border-radius: 4px;
  border: 1px solid #e0e0e6;
  cursor: pointer;
  position: relative; /* 用于内部元素定位 */
  /* 增加左边框过渡效果 */
  transition: border-color 0.2s, box-shadow 0.2s;

  border-left: 4px solid v-bind(highlightColor);
}

/* 当有高亮颜色时，应用颜色到左边框 */
.config-item-base.has-highlight {
  /* 使用 CSS 变量来设置颜色 */
  border-left-color: v-bind(highlightColor);
}

/* 禁用状态的样式 */
.config-item-base.is-disabled {
  opacity: 0.6;
  background-color: #f9f9f9;
}

/* 开关的包裹容器样式 */
.enable-switch-wrapper {
  display: flex;
  align-items: center;
  padding: 0 8px;
  background-color: #fdfdfd;
}

/* 选中状态的样式 */
.config-item-base.is-selected {
  border-color: v-bind(finalHighlightColor);
  box-shadow: 0 0 0 1px v-bind(finalHighlightColor);
}


/* 拖拽区域样式 */
.drag-handle {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 32px; /* 稍宽一点 */
  cursor: grab;
  transition: background-color 0.2s;
  background-color: v-bind(handleBgColor);
  color: v-bind(finalHighlightColor); /* 图标颜色与高亮色一致或默认深灰 */
}


.drag-handle:active {
  cursor: grabbing;
  /* active 状态下背景色加深，提供操作反馈 */
}

/* 内容包装器样式 */
.item-content-wrapper {

  flex-grow: 1; /* 占据所有可用空间 */
  min-width: 0; /* 配合 flex-grow:1 避免内容溢出 */
  display: flex;
  gap: 6px; /* 为内部元素提供间距 */
  /* 在左侧增加内边距，与拖拽柄隔开 */
  padding: 6px 8px;
}

/* 动作区域样式 */
.item-actions {
  display: flex;
  align-items: center;
  margin-left: auto;
  flex-shrink: 0;
  padding: 6px 8px; /* 给予和内容区对称的内边距 */
}
</style>