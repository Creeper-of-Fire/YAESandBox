<!-- Collapse.vue -->
<script lang="ts" setup>
import {NCollapse, NCollapseItem, useThemeVars} from 'naive-ui';
// 导入 ContentRenderer 以进行递归渲染
import ContentRenderer from '../ContentRenderer.vue';

const props = defineProps<{
  name: string;
  'default-open'?: boolean | string;
  rawContent: string;
}>();

const isDefaultOpen = props['default-open'] === true || props['default-open'] === 'true';
const themeVars = useThemeVars();
</script>

<template>
  <NCollapse
      :default-expanded-names="isDefaultOpen ? [props.name] : []"
      class="custom-collapse"
  >
    <NCollapseItem :name="props.name" :title="props.name">
      <!-- 为内部内容添加一个带 padding 的 wrapper -->
      <div class="collapse-content-wrapper">
        <ContentRenderer :content="rawContent"/>
      </div>
    </NCollapseItem>
  </NCollapse>
</template>

<style scoped>
.custom-collapse {
  /* 使用主题变量定义边框和背景 */
  border: 1px solid v-bind('themeVars.borderColor');
  border-radius: v-bind('themeVars.borderRadius');
  background-color: v-bind('themeVars.actionColor'); /* 使用一个非常浅的背景色以示区分 */
  margin: 8px 0; /* 添加垂直外边距 */
  transition: box-shadow 0.3s ease;
}

.custom-collapse:hover {
  box-shadow: v-bind('themeVars.boxShadow1');
}

/* 穿透修改内部样式，去除默认的内外边距和边框 */
.custom-collapse :deep(.n-collapse-item) {
  margin: 0 !important;
}
.custom-collapse :deep(.n-collapse-item__header) {
  padding-left: 12px;
}
.custom-collapse :deep(.n-collapse-item .n-collapse-item__content-wrapper .n-collapse-item__content-inner) {
  padding: 0 !important; /* 清除 naive 默认的内边距 */
}

/* 内容包裹层，提供新的内边距 */
.collapse-content-wrapper {
  padding: 4px 12px 12px 12px;
}
</style>