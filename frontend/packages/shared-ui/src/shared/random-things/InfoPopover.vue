<!-- InfoPopover.vue -->
<template>
  <HoverPinPopover :placement="placement" :max-width="maxWidth">
    <!-- 1. 定义触发器的外观：一个信息图标 -->
    <template #trigger="{ isPinned }">
      <n-icon
          :color="isPinned ? themeVars.primaryColor : undefined"
          :component="InfoIcon"
          :size="size"
          style="cursor: help;"
      />
    </template>

    <!-- 2. 传递内容：优先使用插槽，如果插槽为空则使用 content prop -->
    <slot>{{ content }}</slot>
  </HoverPinPopover>
</template>

<script lang="ts" setup>
import { NIcon, useThemeVars, type PopoverPlacement } from 'naive-ui';
import HoverPinPopover from './HoverPinPopover.vue';
import {InfoIcon} from "../../utils/icons.ts";

/**
 * 一个封装了 HoverPinPopover 的专用信息提示组件。
 * 支持悬停显示、点击固定、点击外部取消固定的高级交互。
 */
withDefaults(defineProps<{
  /**
   * Popover 中显示的默认提示内容 (如果未提供 slot)。
   */
  content?: string;
  /**
   * Popover 的最大宽度。
   */
  maxWidth?: string;
  /**
   * 图标的大小。
   */
  size?: number;
  /**
   * Popover 的弹出位置。
   */
  placement?: PopoverPlacement;
}>(), {
  content: '',
  maxWidth: '300px',
  size: 16,
  placement: 'right',
});

const themeVars = useThemeVars();
</script>