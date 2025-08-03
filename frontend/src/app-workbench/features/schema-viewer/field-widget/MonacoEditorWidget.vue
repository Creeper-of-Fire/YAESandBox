<!-- MonacoEditorWidget.vue -->
<template>
  <div
      ref="editorContainerRef"
      :style="{ height: editorHeight }"
      class="monaco-editor-container"
  >
    <!--
      核心改动：移除 v-show，让编辑器组件的 DOM 结构保持稳定。
      Vue 将不再因为 isLoading 状态的改变而去尝试修改编辑器的显示属性。
    -->
    <vue-monaco-editor
        v-model:value="internalValue"
        :language="language"
        :options="editorOptions"
        :theme="editorTheme"
        @mount="handleEditorMount"
    />

    <!--
      将遮罩层放在编辑器之后，利用 CSS 的 position: absolute
      将它们叠加在编辑器之上。Vue 只需要管理这两个遮罩层的有无即可。
    -->
    <div v-if="isLoading" class="loading-overlay">
      <n-spin size="large"/>
      <span class="loading-text">{{ loadingText }}</span>
    </div>
    <div v-if="error" class="error-overlay">
      <n-alert :show-icon="true" title="编辑器加载失败" type="error">
        {{ error }}
      </n-alert>
    </div>

    <div class="resize-handle" @mousedown="handleResizeStart"></div>
  </div>
</template>
<script lang="ts" setup>
import {computed, onUnmounted, ref, watch} from 'vue';
import {type MonacoEditor, VueMonacoEditor} from '@guolao/vue-monaco-editor';
import {NAlert, NSpin} from 'naive-ui';
import {useDebounceFn} from '@vueuse/core';
import {MonacoLanguageClient} from "monaco-languageclient";

// --- Props ---
const props = defineProps<{
  // 从 v-model 传入
  modelValue: string;

  // 从 ui:options (来自 x-monaco-editor) 传入
  language: string;       // 例如 "lua", "javascript"
  simpleConfigUrl?: string; // 用于简单的补全/悬停配置
  languageServerWorkerUrl?: string; // 用于完整的 LSP

  // 其他可配置项
  height?: string;        // 编辑器高度，例如 "300px"
  theme?: 'vs-dark' | 'light'; // 主题
}>();

// --- Emits ---
const emit = defineEmits(['update:modelValue']);

// --- 内部状态 ---
const internalValue = ref(props.modelValue ?? '');
const isLoading = ref(true); // 默认加载中
const loadingText = ref('正在加载编辑器核心...');
const error = ref<string | null>(null);
const editorHeight = ref(props.height || '400px');
const editorContainerRef = ref<HTMLElement | null>(null);
const editorTheme = computed(() => props.theme || 'vs-dark');

// Monaco Editor 的配置项
const editorOptions = {
  automaticLayout: true, // 自动调整布局
  minimap: {enabled: true},
  wordWrap: 'on' as const,
  scrollBeyondLastLine: false,
};

// 使用防抖来更新 v-model，避免在快速输入时频繁触发
const debouncedUpdate = useDebounceFn((newValue: string) =>
{
  const valueToSet = newValue ?? '';
  if (props.modelValue !== valueToSet)
  {
    emit('update:modelValue', valueToSet);
  }
}, 300);

// 监听内部值的变化，并通知父组件
watch(internalValue, (newValue) =>
{
  debouncedUpdate(newValue);
});

// 监听从父组件传入的值的变化
watch(() => props.modelValue, (newValue) =>
{
  const valueToSet = newValue ?? '';
  if (internalValue.value !== valueToSet)
  {
    internalValue.value = valueToSet;
  }
});

/**
 * 当 Monaco Editor 实例挂载完成后调用
 */
async function handleEditorMount(editor: any, monaco: MonacoEditor)
{
  try
  {
    // 优先处理完整的语言服务器
    if (props.languageServerWorkerUrl)
    {
      loadingText.value = '正在启动语言服务器...';
      await startLanguageServer(monaco, editor.getModel());
    }
    // 其次处理简单的配置
    else if (props.simpleConfigUrl)
    {
      loadingText.value = '正在加载语言配置...';
      await applySimpleConfig(monaco);
    }
  } catch (err: any)
  {
    const errorMessage = `编辑器高级功能配置失败: ${err.message}`;
    console.error(errorMessage, err);
    error.value = errorMessage;
  } finally
  {
    isLoading.value = false;
    loadingText.value = '';
  }
}

let languageClient: MonacoLanguageClient | null = null;
let worker: Worker | null = null;

/**
 * 启动并连接到语言服务器
 */
async function startLanguageServer(monaco: MonacoEditor, model: any)
{
  // TODO 暂时不支持
}

/**
 * 加载并应用简单的配置
 */
async function applySimpleConfig(monaco: MonacoEditor)
{
  const serviceRune = await import(/* @vite-ignore */ props.simpleConfigUrl!);
  if (serviceRune.default && typeof serviceRune.default.configure === 'function')
  {
    serviceRune.default.configure(monaco);
  }
}

// 组件卸载时，清理资源
onUnmounted(() =>
{
  if (languageClient)
  {
    languageClient.stop();
    languageClient = null;
  }
  if (worker)
  {
    worker.terminate();
    worker = null;
  }
});

// --- 拖拽调整大小的逻辑 ---
function handleResizeStart(startEvent: MouseEvent)
{
  startEvent.preventDefault();

  const initialHeight = editorContainerRef.value!.offsetHeight;
  const startY = startEvent.clientY;

  const handleMouseMove = (moveEvent: MouseEvent) =>
  {
    const deltaY = moveEvent.clientY - startY;
    const newHeight = initialHeight + deltaY;
    // 设置一个最小高度，防止拖得太小
    editorHeight.value = `${Math.max(200, newHeight)}px`;
  };

  const handleMouseUp = () =>
  {
    // 清理事件监听器
    window.removeEventListener('mousemove', handleMouseMove);
    window.removeEventListener('mouseup', handleMouseUp);
    // 恢复鼠标样式和文本选择
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
  };

  // 在 window 上添加监听器，以确保鼠标移出元素也能拖拽
  window.addEventListener('mousemove', handleMouseMove);
  window.addEventListener('mouseup', handleMouseUp);

  // 拖拽时优化体验
  document.body.style.cursor = 'ns-resize'; // 设置鼠标样式为上下拖拽
  document.body.style.userSelect = 'none';  // 禁止拖拽时选中页面文本
}

</script>

<style scoped>
.monaco-editor-container {
  position: relative;
  width: 100%;
  border: 1px solid #333; /* 可以根据你的主题调整 */
  border-radius: 4px;
  text-align: left; /* 确保编辑器内容左对齐 */
  overflow: hidden;
}

.loading-overlay,
.error-overlay {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  background-color: rgba(30, 30, 30, 0.8); /* 半透明深色背景 */
  z-index: 10;
  padding: 20px;
  box-sizing: border-box;
}

/* 拖拽手柄的样式 */
.resize-handle {
  position: absolute;
  bottom: 0;
  left: 0;
  width: 100%;
  height: 8px; /* 增加可点击区域 */
  cursor: ns-resize; /* 上下拖动鼠标指针 */
  background: repeating-linear-gradient(
      45deg,
      #555,
      #555 1px,
      transparent 1px,
      transparent 4px
  ) center;
  background-size: 6px 6px;
  opacity: 0.5;
  transition: opacity 0.2s;
}
.resize-handle:hover {
  opacity: 1;
}

.loading-text {
  margin-top: 10px;
  color: #fff;
}
</style>