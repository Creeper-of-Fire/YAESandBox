<!-- src/app-workbench/components/.../ConfigItemBase.vue -->
<template>
  <div
      :class="{ 'is-selected': isSelected , 'is-disabled': !enabled || isParentDisabled }"
      class="config-item-base"
      @click="$emit('click')"
      @dblclick.prevent="$emit('dblclick')"
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

    <!-- 主内容区插槽 -->
    <div class="item-content-wrapper">
      <slot name="content"></slot>
    </div>

    <div class="item-actions">
      <!-- 动作插槽：可用于放置编辑按钮、删除按钮、更多操作菜单等 -->
      <slot name="actions"></slot>
    </div>

    <div class="enable-switch-wrapper" @click.stop>
      <n-switch
          :round="false"
          :value="enabled"
          class="compact-switch"
          size="small"
          @update:value="(newValue:boolean) => $emit('update:enabled', newValue)"
      >
      </n-switch>
    </div>

  </div>
</template>

<script lang="ts" setup>
import {NIcon, useThemeVars} from 'naive-ui';
import {DragHandleOutlined} from '@yaesandbox-frontend/shared-ui/icons';
import {computed, inject, ref} from "vue";
import ColorHash from "color-hash";
import {IsParentDisabledKey} from "@/utils/injectKeys.ts";
import {IsDarkThemeKey} from "@yaesandbox-frontend/core-services/injectKeys";

// 定义组件的 props
const props = defineProps<{
  isCollapsible?: boolean; // 是否可以展开下面的内容
  isSelected: boolean; // 是否处于选中状态
  isDraggable?: boolean; // 是否可拖拽（显示拖拽把手）
  highlightColorCalculator: string;
  enabled: boolean;
}>();

const isParentDisabled = inject(IsParentDisabledKey, ref(false));

const themeVars = useThemeVars();

const isDarkTheme = computed(() => inject(IsDarkThemeKey)?.value);

/**
 * 判断当前是否为深色模式的正确方法。
 * 我们通过检查一个基础背景色（如 cardColor）的亮度来判断。
 * 这里简单地假设 #fff 系列为浅色，其他为深色。
 * 一个更鲁棒的方法是计算颜色的亮度，但对于 Naive UI 的默认主题，
 * 检查第一个字符是否为 #f 就足够了。
 */
const isDarkMode = computed(() =>
{
  if (isDarkTheme.value !== undefined)
    return isDarkTheme.value;
  // themeVars.value.cardColor 在浅色模式下通常是 '#ffffff'，深色模式是 '#18181c'
  // 我们可以简单地检查颜色值。一个简单的技巧是检查它是否“亮”。
  // 这里我们用一个简化的方法：如果颜色不是以 '#ff' 开头，就认为是深色。
  // 在实践中，这对于默认主题是足够可靠的。
  const color = themeVars.value.cardColor.toLowerCase();
  return !color.startsWith('#fff');
});

// 创建一个响应式的 ColorHash 实例
// 这个计算属性会在主题变化时重新运行，返回一个为新主题配置好的新实例
const colorHashInstance = computed(() =>
{

  if (isDarkMode.value)
  {
    // 深色模式配置：颜色更亮、饱和度稍低，以在深色背景上更柔和
    return new ColorHash({
      lightness: [0.70, 0.75, 0.80],
      saturation: [0.45, 0.55, 0.65], // 降低饱和度避免刺眼
      hash: 'bkdr'
    });
  }
  else
  {
    // 浅色模式配置：颜色更深、饱和度更高，以在浅色背景上更突出
    return new ColorHash({
      lightness: [0.50, 0.55, 0.60], // 降低亮度使其变深
      saturation: [0.65, 0.75, 0.85], // 保持或增加饱和度
      hash: 'bkdr'
    });
  }
});

const highlightColor = computed(() =>
{
  return colorHashInstance.value.hex(props.highlightColorCalculator);
});

// 定义组件触发的事件
defineEmits(['click', 'dblclick', 'update:enabled']);

/**
 * 计算属性，用于处理选中状态下的边框颜色。
 * 如果 highlightColor 存在，则使用它，否则回退到默认的蓝色主题色。
 */
const finalHighlightColor = computed(() => highlightColor.value || themeVars.value.primaryColor);

/**
 * 计算属性，用于拖拽区域的背景色
 * 如果没有提供 highlightColor，则使用一个柔和的灰色作为默认值
 */
const handleBgColor = computed(() => highlightColor.value ? `${highlightColor.value}33` : themeVars.value.actionColor);

// 使用 CSS 变量将主题色暴露给 <style> 块
const borderColor = computed(() => themeVars.value.borderColor);
const cardColor = computed(() => themeVars.value.cardColor);
const baseColor = computed(() => themeVars.value.baseColor);
const textColorDisabled = computed(() => themeVars.value.textColorDisabled);
const actionColor = computed(() => themeVars.value.actionColor);
const primaryColor = computed(() => themeVars.value.primaryColor);
</script>

<style scoped>
/* 基础样式，所有可配置项的通用外观 */
.config-item-base {
  display: flex;
  align-items: stretch;
  padding: 0;
  background-color: v-bind(cardColor);
  border-radius: 4px;
  margin: 2px;
  margin-bottom: 0;
  border: 1px solid v-bind(borderColor);
  cursor: pointer;
  position: relative; /* 用于内部元素定位 */
  /* 增加左边框过渡效果 */
  transition: border-color 0.2s, box-shadow 0.2s, opacity 0.3s, background-color 0.3s;
  border-left: 3px solid v-bind(highlightColor);
}

/* 禁用状态的样式 */
.config-item-base.is-disabled {
  opacity: 0.5;
}

/* 开关的包裹容器样式 */
.enable-switch-wrapper {
  display: flex;
  align-items: center;
  border-left: 1px solid v-bind(borderColor);
}

/* 缩小开关的样式 */
.compact-switch {
  transform: scale(0.6);
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
  padding: 4px 8px;
}

/* 动作区域样式 */
.item-actions {
  display: flex;
  align-items: center;
  margin-left: auto;
  flex-shrink: 0;
  padding: 0 6px; /* 给予和内容区对称的内边距 */
  gap: 6px;
}
</style>