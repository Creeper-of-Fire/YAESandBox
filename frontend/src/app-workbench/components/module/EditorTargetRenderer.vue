<!-- src/app-workbench/components/.../EditorTargetRenderer.vue -->
<template>
  <div class="editor-target-renderer">
    <!-- 现在直接使用 selectedModuleId prop -->
    <div v-if="selectedModuleId">
      <n-h4>配置模块 (待实现)</n-h4>
      <p>选中的模块ID: {{ selectedModuleId }}</p>
      <!-- TODO: 在这里放置 DynamicFormRenderer 组件 -->
    </div>
    <n-empty v-else description="请在左侧结构树中选择一个模块进行配置"/>
  </div>
</template>

<script setup lang="ts">
import {NEmpty, NH4} from 'naive-ui';
import type {EditSession} from "@/app-workbench/services/EditSession.ts";

const props = defineProps<{
  session: EditSession; // session 仍然需要，用于将来可能的保存等操作
  selectedModuleId: string | null; // 接收来自父组件的 selectedModuleId
}>();

// 不再需要从 session 获取选中项
</script>

<style scoped>
.editor-target-renderer {
  background-color: #fff;
  padding: 24px;
  height: 100%;
  box-sizing: border-box;
  border-radius: 8px;
}
</style>
<!-- END OF FILE -->