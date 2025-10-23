<!-- components/SmartEditor.vue -->

<template>
  <div class="smart-editor-wrapper">
    <!-- 编辑器右上角的控件区域 -->
    <div class="editor-controls">
      <slot name="controls">
        <!-- 默认提供一个模式切换开关 -->
        <n-flex align="center" size="small">
          <label :for="switchId" class="control-label">高级编辑模式</label>
          <n-switch :id="switchId" v-model:value="isUseMonaco"/>
        </n-flex>
      </slot>
    </div>

    <!-- 编辑器主体 -->
    <div class="editor-container">
      <VueMonacoEditor
          v-if="isUseMonaco"
          :key="editorTheme"
          v-model:value="localValue"
          :height="editorHeight"
          :language="language"
          :options="monacoOptions"
          :theme="editorTheme"
          class="monaco-instance"
          @mount="handleEditorMount"
      />
      <n-input
          v-else
          v-model:value="localValue"
          :autosize="{ minRows: 10 }"
          :style="{ height: editorHeight }"
          class="textarea-instance"
          placeholder="请输入代码..."
          type="textarea"
      />
    </div>
  </div>
</template>

<script lang="ts" setup>
import {computed, inject, onBeforeUnmount, ref, watch} from 'vue';
import {nanoid} from 'nanoid';
import {NFlex, NInput, NSwitch, useThemeVars} from 'naive-ui';
import {type MonacoEditor, VueMonacoEditor} from '@guolao/vue-monaco-editor';
import {useScopedStorage} from '@yaesandbox-frontend/core-services/composables';
import {IsDarkThemeKey} from '@yaesandbox-frontend/core-services/inject-key';
import {useVModel} from '@vueuse/core';
import type {MonacoLanguageEnhancer} from "./types.ts";

interface ExtraLib
{
  content: string;
  filePath: string;
}

const props = defineProps<{
  modelValue: string;
  storageKey: string; // 用于持久化切换状态的唯一键
  language?: 'javascript' | 'typescript' | 'json' | string;
  extraLibs?: ExtraLib[];
  height?: string | number;
  enhancer?: MonacoLanguageEnhancer;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void;
}>();

const localValue = useVModel(props, 'modelValue', emit);

const themeVars = useThemeVars();

// --- 模式切换 ---
const isUseMonaco = useScopedStorage(props.storageKey, true); // 默认开启Monaco
const switchId = `editor-mode-switch-${nanoid(5)}`;

// --- 主题和编辑器选项 ---
const isCurrentlyDark = inject(IsDarkThemeKey, ref(false));
const editorTheme = computed(() => (isCurrentlyDark.value ? 'vs-dark' : 'light'));
const monacoOptions = {
  automaticLayout: true,
  minimap: {enabled: true},
  wordWrap: 'on' as const,
  scrollBeyondLastLine: false,
  fixedOverflowWidgets: true,
  fontSize: parseInt(themeVars.value.fontSizeMedium, 10),   // 将字体大小与主题变量关联
  tabSize: 2,
  // 也可以在这里设置字体，但CSS中设置更统一
  // fontFamily: themeVars.value.fontFamilyMono,
};


// --- 尺寸 ---
// 如果父组件没有提供高度，就自适应
const editorHeight = computed(() =>
{
  if (typeof props.height === 'number')
  {
    return `${props.height}px`;
  }
  return props.height || '400px';
});

const editorInstance = ref<MonacoEditor | null>(null);
const monacoInstance = ref<MonacoEditor | null>(null);
let activeEnhancer: MonacoLanguageEnhancer | null = null;

// --- 状态管理 ---
const handleEditorMount = (editor: any, monaco: MonacoEditor) =>
{
  editorInstance.value = editor; // 保存编辑器实例
  monacoInstance.value = monaco;

  // 当编辑器挂载时，如果传入了 enhancer，就使用它
  if (props.enhancer)
  {
    activeEnhancer = props.enhancer;
    activeEnhancer.setup(editor, monaco, props.extraLibs || []);
  }
};

onBeforeUnmount(() =>
{
  // 组件卸载时，清理当前的 enhancer
  if (activeEnhancer)
  {
    activeEnhancer.dispose();
    activeEnhancer = null;
  }
});
</script>

<style scoped>
.smart-editor-wrapper {
  position: relative;
  border: 1px solid v-bind('themeVars.borderColor');
  border-radius: v-bind('themeVars.borderRadius');
  background-color: v-bind('themeVars.cardColor');
  padding: 8px;
  padding-top: 40px; /* 为控件留出空间 */
  transition: border-color .3s, background-color .3s;
}

.editor-controls {
  position: absolute;
  top: 4px;
  right: 8px;
  z-index: 10;
  display: flex;
  align-items: center;
  gap: 12px;
}

.control-label {
  font-size: v-bind('themeVars.fontSizeSmall');
  color: v-bind('themeVars.textColor3');
  cursor: pointer;
  white-space: nowrap;
  transition: color .3s;
}

.editor-container {
  /* overflow: hidden; */
  border-radius: 3px;
}

.monaco-instance,
.textarea-instance {
  width: 100%;
  font-family: v-bind('themeVars.fontFamilyMono');
  font-size: v-bind('themeVars.fontSizeMedium'); /* 确保 n-input 和 monaco 字体大小一致 */
  line-height: v-bind('themeVars.lineHeight');
}

/* 针对 n-input 的一些微调，使其更像代码编辑器 */
.textarea-instance {
  :deep(textarea) {
    line-height: inherit; /* 继承我们设置的行高 */
  }
}
</style>