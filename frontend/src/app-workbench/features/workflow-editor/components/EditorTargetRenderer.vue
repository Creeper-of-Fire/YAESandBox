<!-- START OF FILE: src/app-workbench/features/components/EditorTargetRenderer.vue -->
<template>
  <div class="editor-target-renderer">
    <div v-if="session">
      <div v-if="selectedModuleId">
        <n-h4>配置模块 (待实现)</n-h4>
        <p>选中的模块ID: {{ selectedModuleId }}</p>
        <!-- TODO: 在这里放置 DynamicFormRenderer 组件 -->
      </div>
      <n-empty v-else description="请在左侧结构树中选择一个模块进行配置"/>
    </div>
  </div>
</template>

<script setup lang="ts">
import {computed} from 'vue';
import {NEmpty, NH4} from 'naive-ui';
import type {EditSession} from "@/app-workbench/features/workflow-editor/services/EditSession.ts";

const props = defineProps<{
  session: EditSession;
}>();

// 从 session 中获取响应式的选中项ID
const selectedModuleId = computed(() => props.session.getSelectedItemId().value);

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