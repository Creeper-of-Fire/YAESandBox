<template>
  <n-flex class="game-layout" :wrap="false">
    <!-- 左侧边栏容器 -->
    <div class="layout-sider" :style="leftSiderStyle">
      <slot name="left-sider"></slot>
    </div>

    <!-- 中间主内容区容器 -->
    <div class="layout-content" :style="contentStyle">
      <slot name="content"></slot>
    </div>

    <!-- 右侧边栏容器 -->
    <div class="layout-sider" :style="rightSiderStyle">
      <slot name="right-sider"></slot>
    </div>
  </n-flex>
</template>

<script lang="ts" setup>
import { computed } from 'vue';
import { NFlex, useThemeVars } from 'naive-ui';

const themeVars = useThemeVars();

// 左侧边栏的样式
const leftSiderStyle = computed(() => ({
  width: '240px', // 固定宽度
  backgroundColor: themeVars.value.cardColor,
  borderRight: `1px solid ${themeVars.value.borderColor}`,
}));

// 中间内容区的样式
const contentStyle = computed(() => ({
  flex: 1, // 占据剩余所有空间
  minWidth: 0, // 防止内容溢出时破坏flex布局
  backgroundColor: themeVars.value.bodyColor,
}));

// 右侧边栏的样式
const rightSiderStyle = computed(() => ({
  width: '260px', // 固定宽度
  backgroundColor: themeVars.value.cardColor,
  borderLeft: `1px solid ${themeVars.value.borderColor}`,
}));
</script>

<style scoped>
.game-layout {
  width: 100%;
  height: 100%;
  overflow: hidden; /* 防止子元素超出容器 */
}

/*
  为侧边栏和内容区设置flex容器属性，
  确保其内部的插槽内容能正确地拉伸和滚动。
*/
.layout-sider,
.layout-content {
  height: 100%;
  display: flex;
  flex-direction: column;
  /* 如果内部内容可能溢出，overflow: auto 会很有用 */
  overflow: hidden;
}
</style>