<!-- Collapse.vue -->
<script setup lang="ts">
import { NCollapse, NCollapseItem } from 'naive-ui';
// 导入 ContentRenderer 以进行递归渲染
import ContentRenderer from '../ContentRenderer.vue';

const props = defineProps<{
  name: string;
  'default-open'?: boolean | string;
  rawContent: string;
}>();

const isDefaultOpen = props['default-open'] === true || props['default-open'] === 'true';
</script>

<template>
  <NCollapse :default-expanded-names="isDefaultOpen ? [props.name] : []">
    <NCollapseItem :title="props.name" :name="props.name">
      <ContentRenderer :content="rawContent" />
    </NCollapseItem>
  </NCollapse>
</template>