<!-- era-lite/src/components/BaseItemDisplay.vue -->
<template>
  <n-list-item>
    <n-thing
        :title="title"
        :description="description"
        :description-style="{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }"
        :content-style="{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }"
        :header-style="{ whiteSpace: 'normal', wordBreak: 'break-word' }"
        style="white-space: pre-wrap;"
    />
    <template #suffix>
      <!-- 将操作按钮通过插槽暴露出去，让父组件决定放什么 -->
      <n-flex>
        <slot name="actions"></slot>
      </n-flex>
    </template>
  </n-list-item>
</template>

<script setup lang="ts">
import { NListItem, NThing, NFlex } from 'naive-ui';

defineProps<{
  title: string;
  description: string;
}>();
</script>