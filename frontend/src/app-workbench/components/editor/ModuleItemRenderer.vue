<!-- src/app-workbench/components/.../ModuleItemRenderer.vue -->
<template>
  <ConfigItemBase
      :is-selected="selectedModuleId === module.configId"
      :name="module.name"
      is-draggable
      @click="$emit('update:selectedModuleId', module.configId)"
  />
</template>

<script lang="ts" setup>
import ConfigItemBase from './ConfigItemBase.vue';
import type {AbstractModuleConfig} from '@/app-workbench/types/generated/workflow-config-api-client';

// 定义 Props 和 Emits
defineProps<{
  module: AbstractModuleConfig;
  selectedModuleId: string | null;
}>();

defineEmits(['update:selectedModuleId']);
</script>

<!-- 这个组件不需要自己的 style 标签，因为它只是 ConfigItemBase 的一个薄封装，样式由 ConfigItemBase 提供 -->