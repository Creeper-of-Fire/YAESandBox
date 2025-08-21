<template>
  <slot />
</template>

<script setup lang="ts">
import { provide } from 'vue';
import { PluginUniqueNameKey } from '@yaesandbox-frontend/core-services/injectKeys';

// 接收由路由元信息传入的插件 ID
const props = defineProps<{
  pluginUniqueName: string;
}>();

// 在这个组件的层级上 provide ID
provide(PluginUniqueNameKey, props.pluginUniqueName);
</script>