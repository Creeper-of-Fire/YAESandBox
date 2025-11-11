<!-- HoverPinPopover.vue -->
<template>
  <n-popover
      :show="isPopoverVisible"
      :style="{maxWidth: maxWidth}"
      :trigger="'manual'"
      :placement="placement"
      @clickoutside="handleClickOutside"
  >
    <template #trigger>
      <!-- 触发器容器，绑定所有交互事件 -->
      <div
          @mouseenter="handleMouseEnter"
          @mouseleave="handleMouseLeave"
          @click.stop="handleTriggerClick"
      >
        <!-- 使用作用域插槽，将内部状态 'isPinned' 暴露给父组件 -->
        <slot :is-pinned="isPopoverPinned" name="trigger"></slot>
      </div>
    </template>

    <!-- 内容容器，同样绑定悬停事件以创建“安全区域” -->
    <div
        @mouseenter="handleMouseEnter"
        @mouseleave="handleMouseLeave"
    >
      <!-- 默认插槽，用于填充 Popover 的内容 -->
      <slot></slot>
    </div>
  </n-popover>
</template>

<script lang="ts" setup>
import {ref} from 'vue';
import {NPopover, type PopoverPlacement} from 'naive-ui';

// --- Props 定义 ---
withDefaults(defineProps<{
  placement?: PopoverPlacement;
  maxWidth?: string;
}>(), {
  placement: 'right-start',
  maxWidth: '400px',
});

// --- 状态定义 ---

// 控制 Popover 是否可见的最终开关
const isPopoverVisible = ref(false);

// 标记 Popover 是否被用户点击“固定”
const isPopoverPinned = ref(false);

// 用于延迟隐藏 Popover 的定时器ID
let hideTimer: number | null = null;


// --- 事件处理函数 ---

/**
 * 当鼠标进入任何一个“安全区域”（触发器或浮层内容）时调用。
 * 主要作用是：
 * 1. 如果有计划中的隐藏任务，则取消它。
 * 2. 确保浮层是可见的。
 */
function handleMouseEnter()
{
  if (hideTimer)
  {
    clearTimeout(hideTimer);
    hideTimer = null;
  }
  isPopoverVisible.value = true;
}

/**
 * 当鼠标离开任何一个“安全区域”时调用。
 * 如果浮层未被固定，则启动一个短暂的延迟后隐藏它。
 * 这种延迟可以防止鼠标在触发器和浮层之间快速移动时意外关闭。
 */
function handleMouseLeave()
{
  // 如果已经被用户点击固定，则什么都不做
  if (isPopoverPinned.value)
  {
    return;
  }
  // 启动一个延迟隐藏
  hideTimer = window.setTimeout(() =>
  {
    isPopoverVisible.value = false;
  }, 200);
}

/**
 * 当用户点击触发器时调用。
 * 主要作用是切换“固定”状态。
 */
function handleTriggerClick()
{
  // 1. 清除任何可能存在的隐藏定时器，确保点击后不会意外关闭
  if (hideTimer)
  {
    clearTimeout(hideTimer);
    hideTimer = null;
  }

  // 2. 切换固定状态
  isPopoverPinned.value = !isPopoverPinned.value;

  // 3. 确保 Popover 在点击后是可见的。如果取消固定，则它将遵循悬停逻辑。
  isPopoverVisible.value = true;
}

/**
 * 当用户点击 Popover 外部区域时调用 (由 n-popover 自身提供)。
 * 只有在浮层被“固定”时，此操作才有效，用于取消固定状态。
 */
function handleClickOutside()
{
  // 只有在固定的情况下，点击外部才生效
  if (isPopoverPinned.value)
  {
    // 使用一个非常短的延迟来避免一些事件冲突
    hideTimer = window.setTimeout(() =>
    {
      isPopoverPinned.value = false;
      isPopoverVisible.value = false;
    }, 100);
  }
}

/**
 使用方法：
 <template>
 <HoverPinPopover>
 <!-- 触发器插槽，可以根据 isPinned 状态改变样式 -->
 <template #trigger="{ isPinned }">
 <n-button :type="isPinned ? 'primary' : 'default'">
 {{ isPinned ? '已固定' : '悬停或点击我' }}
 </n-button>
 </template>

 <!-- 默认插槽，放置浮层内容 -->
 <p>这里是浮层里的详细信息。</p>
 <p>鼠标可以自由地移动到这里而不会关闭我。</p>
 </HoverPinPopover>
 </template>
 */
</script>