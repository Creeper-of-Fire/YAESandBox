<template>
  <div :style="{ height: editorHeight }" class="monaco-editor-container">
    <div v-if="isLoading" class="loading-overlay">
      <n-spin size="large"/>
      <span class="loading-text">{{ loadingText }}</span>
    </div>
    <div v-if="error" class="error-overlay">
      <n-alert :show-icon="true" title="编辑器加载失败" type="error">
        {{ error }}
      </n-alert>
    </div>

    <!-- 使用 @guolao/vue-monaco-editor 组件 -->
    <!-- 使用 v-show 可以在加载时保持 DOM 结构，避免布局跳动 -->
    <vue-monaco-editor
        v-show="!isLoading && !error"
        v-model:value="internalValue"
        :language="language"
        :options="editorOptions"
        :theme="editorTheme"
        @mount="handleEditorMount"
    />
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
const internalValue = ref(props.modelValue);
const isLoading = ref(true); // 默认加载中
const loadingText = ref('正在加载编辑器核心...');
const error = ref<string | null>(null);
const editorHeight = computed(() => props.height || '300px');
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
  if (props.modelValue !== newValue)
  {
    emit('update:modelValue', newValue);
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
  if (internalValue.value !== newValue)
  {
    internalValue.value = newValue;
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

  // return new Promise<void>((resolve, reject) =>
  // {
  //   // 创建一个 Web Worker 来运行语言服务器
  //   // URL() 构造函数确保相对路径被正确解析
  //    worker = new Worker(new URL(props.languageServerWorkerUrl!, import.meta.url).href, {type: 'module'});
  //   worker.onerror = (e) =>
  //   {
  //     reject(new Error(`Worker 错误: ${e.message}`));
  //   };
  //
  //   // !!! 关键改动：使用 createWebWorkerConnection !!!
  //   // 它会返回一个 MessageConnection 对象，其中包含了 reader 和 writer
  //   const connection = createWebWorkerConne(worker);
  //   const transports: MessageTransports = {reader: connection.reader, writer: connection.writer};
  //
  //   // 确保 Monaco 也知道这个语言
  //   monaco.languages.register({id: props.language});
  //
  //   // 创建语言客户端
  //    languageClient = new MonacoLanguageClient({
  //     name: `${props.language.toUpperCase()} Language Client`,
  //     clientOptions: {
  //       documentSelector: [props.language], // 告诉客户端这个服务只对 'lua' 文件生效
  //       errorHandler: {
  //         error: () => ({action: ErrorAction.Continue}),
  //         closed: () => ({action: CloseAction.DoNotRestart}),
  //       },
  //       // !!! 关键：在这里可以传递初始化选项给语言服务器 !!!
  //       // 例如，我们可以告诉 Lua LS 我们的 `ctx` 是一个已知的全局变量
  //       initializationOptions: {
  //         globals: ['ctx']
  //       }
  //     },
  //     connectionProvider: {
  //       get: () => Promise.resolve(transports),
  //     },
  //   });
  //
  //   // 启动客户端
  //   languageClient.start().then(() =>
  //   {
  //     console.log(`语言服务器 [${props.language}] 已成功启动并连接。`);
  //     // 当连接成功后，还需要设置 Monaco Editor 的语言模型，这通常在 connect 内部完成
  //     // 或者在 MonacoLanguageClient 的构造函数中通过 options.diagnosticCollectionName 等方式关联
  //     // 对于 sumneko/lua-language-server，它通常会自动与 Monaco 的模型关联
  //     resolve();
  //   }).catch(e => reject(new Error(`语言客户端启动失败: ${e}`)));
  // });
}

/**
 * 加载并应用简单的配置 (我们之前的方案)
 */
async function applySimpleConfig(monaco: MonacoEditor)
{
  const serviceModule = await import(/* @vite-ignore */ props.simpleConfigUrl!);
  if (serviceModule.default && typeof serviceModule.default.configure === 'function')
  {
    serviceModule.default.configure(monaco);
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
</script>

<style scoped>
.monaco-editor-container {
  position: relative;
  width: 100%;
  border: 1px solid #333; /* 可以根据你的主题调整 */
  border-radius: 4px;
  text-align: left; /* 确保编辑器内容左对齐 */
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

.loading-text {
  margin-top: 10px;
  color: #fff;
}
</style>