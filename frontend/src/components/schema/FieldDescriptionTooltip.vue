<template>
  <n-tooltip v-if="description" trigger="hover">
    <template #trigger>
      <n-icon name="information-circle-outline" />
    </template>
    {{ description }}
  </n-tooltip>
</template>

<script setup lang="ts">
import { NIcon, NTooltip } from 'naive-ui';

defineProps({
  description: {
    type: String as () => string | null | undefined,
    default: null,
  },
});
</script>