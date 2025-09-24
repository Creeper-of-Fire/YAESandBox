<!-- src/app-workbench/components/.../ConfigItemBase.vue -->
<template>
  <div
      ref="rootElement"
      v-lazy-render="renderActions"
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
      <slot :title-class="$style.title" name="content"></slot>
    </div>

    <div class="item-actions">
      <!-- 使用 v-if 控制插槽的渲染 -->
      <template v-if="shouldRenderActions">
        <slot name="actions"></slot>
      </template>
    </div>

    <div v-if="!hiddenSwitch" class="enable-switch-wrapper" @click.stop>
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
import {computed, inject, ref, toRefs} from "vue";
import {IsParentDisabledKey} from "#/utils/injectKeys.ts";
import {vLazyRender} from '@yaesandbox-frontend/shared-ui'
import {useColorHash} from "#/components/share/renderer/useColorHash.ts";

// 定义组件的 props
const props = defineProps<{
  isCollapsible?: boolean; // 是否可以展开下面的内容
  isSelected: boolean; // 是否处于选中状态
  isDraggable?: boolean; // 是否可拖拽（显示拖拽把手）
  highlightColorCalculator: string;
  enabled: boolean;
  hiddenSwitch?: boolean;
}>();

const rootElement = ref<HTMLElement | null>(null);
const shouldRenderActions = ref(false);

const renderActions = () => { shouldRenderActions.value = true; };

const isParentDisabled = inject(IsParentDisabledKey, ref(false));

const themeVars = useThemeVars();

const {highlightColorCalculator} = toRefs(props);

const {color: highlightColor} = useColorHash(highlightColorCalculator);

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

<style module>
.title {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  min-width: 0;
  width: 100%;
  display: block;
}
</style>