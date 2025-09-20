<template>
  <div class="editor-layout">
    <!-- 左侧：代码编辑区 -->
    <n-card title="JSX Component Editor" class="editor-pane">
      <n-input
          v-model:value="componentId"
          placeholder="Component Tag Name (e.g., my-button)"
          style="margin-bottom: 16px;"
      />
      <n-input
          v-model:value="sourceCode"
          type="textarea"
          placeholder="Write your JSX code here..."
          :rows="20"
          style="font-family: monospace;"
      />
      <template #footer>
        <n-button type="primary" @click="handleCompile" :loading="isCompiling">
          Compile & Register
        </n-button>
        <n-alert v-if="errorMessage" type="error" style="margin-top: 16px;">
          {{ errorMessage }}
        </n-alert>
      </template>
    </n-card>

    <!-- 右侧：实时预览区 -->
    <n-card title="Live Preview & Test Case" class="preview-pane">
      <template #header-extra>
        <n-tag type="info">Status: {{ componentState?.status || 'idle' }}</n-tag>
      </template>

      <!-- 测试用例文本域 -->
      <n-h6>Test Content for ContentRenderer</n-h6>
      <n-input
          v-model:value="testContent"
          type="textarea"
          placeholder="Write the content to be rendered..."
          :rows="8"
          style="font-family: monospace; margin-bottom: 16px;"
      />

      <n-h6>Live Preview</n-h6>
      <div v-if="componentState?.status === 'ready'">
        <div class="preview-box">
          <ContentRenderer :content="testContent" />
        </div>
      </div>
      <div v-else>
        <p>Compile the component to see a preview.</p>
      </div>
    </n-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { useComponentStore } from '../stores/useComponentStore';
import { NCard, NInput, NButton, NAlert } from 'naive-ui';
// 导入我们强大的 ContentRenderer
import { ContentRenderer } from '@yaesandbox-frontend/shared-ui/content-renderer';
import {exampleCode, exampleContent,exampleName} from "#/views/example.ts";

const store = useComponentStore();

// 编辑器状态
const testContent = ref(exampleContent);
const componentId = ref(exampleName);
const sourceCode = ref(exampleCode);

// 从 store 派生计算属性
const componentState = computed(() => store.components.get(componentId.value.toLowerCase()));
const isCompiling = computed(() => componentState.value?.status === 'compiling');
const errorMessage = computed(() => componentState.value?.error);

// 预览内容
const previewContent = computed(() => `<${componentId.value}></${componentId.value}>`);

// 事件处理
const handleCompile = () => {
  if (!componentId.value || !sourceCode.value) {
    alert('Component tag name and source code cannot be empty.');
    return;
  }
  store.addOrUpdateComponent(componentId.value, sourceCode.value);
};
</script>

<style scoped>
.editor-layout {
  display: flex;
  gap: 16px;
  padding: 16px;
}
.editor-pane, .preview-pane {
  flex: 1;
}
.preview-box {
  border: 1px dashed #ccc;
  padding: 16px;
  border-radius: 4px;
  min-height: 200px;
}
</style>