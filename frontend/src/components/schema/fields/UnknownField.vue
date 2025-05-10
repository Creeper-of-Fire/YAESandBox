<template>
  <n-text type="error">
    不支持的字段类型: {{ fieldName }} ({{ fieldType }})
  </n-text>
</template>

<script setup lang="ts">
import { NText } from 'naive-ui';

defineProps({
  fieldName: {
    type: String,
    required: true,
  },
  fieldType: {
    type: String,
    required: true,
  }
});
</script>