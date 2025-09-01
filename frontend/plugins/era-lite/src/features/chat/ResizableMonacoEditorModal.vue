<!-- ResizableMonacoEditorModal.vue -->
<template>
  <n-modal
      :show="show"
      :style="modalStyle"
      preset="card"
      @update:show="$emit('update:show', $event)"
  >
    <template #header>
      <n-space align="center" justify="space-between" style="width: 100%;">
        <span>{{ title }}</span>
        <n-space :size="12" align="center">
          <!-- Monaco模式切换 -->
          <n-flex align="center" size="small">
            <label :for="switchId" style="font-size: 12px; color: #999; cursor: pointer; white-space: nowrap;">高级编辑模式</label>
            <n-switch :id="switchId" v-model:value="isUseMonaco"/>
          </n-flex>

          <n-button
              :title="isModalFullscreen ? '退出全屏' : '全屏'"
              circle
              quaternary
              @click="isModalFullscreen = !isModalFullscreen"
          >
            <template #icon>
              <n-icon :component="isModalFullscreen ? FullscreenExitIcon : FullscreenIcon"/>
            </template>
          </n-button>
        </n-space>
      </n-space>
    </template>

    <div style="position: relative;">
      <n-flex size="large" vertical>
        <slot name="header-alert">
          <n-alert type="warning">
            您正在编辑将在此浏览器中执行的JavaScript代码。请不要运行您不信任的代码。
          </n-alert>
          <n-alert type="info">
            请在下方定义一个名为 <code>{{ expectedFunctionName }}</code> 的函数。
            它将接收一个字符串参数 <code>content</code> 并必须返回一个字符串。
          </n-alert>
        </slot>

        <VueMonacoEditor
            v-if="isUseMonaco"
            v-model:value="scriptInEditor"
            :height="editorHeight"
            :options="monacoOptions"
            :style="{ border: `1px solid ${isCurrentlyDark ? '#333' : '#ddd'}`, borderRadius: '4px' }"
            :theme="editorTheme"
            language="javascript"
        />
        <n-input
            v-else
            v-model:value="scriptInEditor"
            :autosize="{minRows: 3}"
            :style="{ height: editorHeight, fontFamily: 'monospace' }"
            placeholder="请输入脚本..."
            type="textarea"
        />

        <n-alert v-if="editorScriptError" title="实时编译错误" type="error">
          {{ editorScriptError }}
        </n-alert>

        <details>
          <summary style="cursor: pointer">查看默认脚本示例</summary>
          <n-code :code="defaultScript" language="javascript" word-wrap/>
        </details>
      </n-flex>

      <!-- 拖拽手柄 -->
      <div
          v-if="!isModalFullscreen"
          class="resize-handle"
          @mousedown="handleMouseDown"
      />
    </div>

    <template #action>
      <n-space justify="end">
        <n-button @click="handleReset">重置为默认</n-button>
        <n-button
            type="primary"
            @click="handleSave">
          保存并关闭
        </n-button>
      </n-space>
    </template>
  </n-modal>
</template>

<script lang="ts" setup>
import {computed, inject, ref, watch} from 'vue';
import {nanoid} from 'nanoid';
import {NAlert, NButton, NCode, NFlex, NIcon, NModal, NSpace} from 'naive-ui';
import {VueMonacoEditor} from '@guolao/vue-monaco-editor';
import {useScopedStorage} from '@yaesandbox-frontend/core-services/composables';
import {IsDarkThemeKey} from '@yaesandbox-frontend/core-services/injectKeys';
import {FullscreenExitIcon, FullscreenIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {useScriptCompiler} from "#/features/chat/useScriptCompiler.ts";

const props = defineProps<{
  show: boolean;
  script: string;
  defaultScript: string;
  storageKeyPrefix: string;
  title?: string;
  expectedFunctionName: string;
}>();

const emit = defineEmits<{
  (e: 'update:show', value: boolean): void;
  (e: 'update:script', value: string): void;
}>();

// --- 内部状态 ---
const scriptInEditor = ref('');
const isModalFullscreen = ref(false);
const isUseMonaco = useScopedStorage(`${props.storageKeyPrefix}-use-monaco-editor-mode`, false);
const switchId = `downgrade-switch-${nanoid(5)}`;

// --- 脚本验证逻辑 ---
const {error: editorScriptError} = useScriptCompiler({
  scriptRef: scriptInEditor, // 将内部的 ref 传递给 Composable
  expectedFunctionName: props.expectedFunctionName,
});

// --- 主题和编辑器选项 ---
const isCurrentlyDark = inject(IsDarkThemeKey, ref(false));
const editorTheme = computed(() => (isCurrentlyDark.value ? 'vs-dark' : 'light'));
const monacoOptions = {
  automaticLayout: true,
  minimap: {enabled: false},
  wordWrap: 'on' as const,
  scrollBeyondLastLine: false,
};

// --- 尺寸控制 ---
const modalWidth = useScopedStorage(`${props.storageKeyPrefix}-modal-width`, 800);
const minWidth = 500;
const editorHeight = computed(() => (isModalFullscreen.value ? 'calc(95vh - 260px)' : '400px'));
const modalStyle = computed(() => (
    isModalFullscreen.value
        ? {width: '95vw', maxWidth: '95vw'}
        : {width: `${modalWidth.value}px`, minWidth: `${minWidth}px`}
));

// --- 同步父组件传入的 script ---
watch(() => props.show, (isVisible) =>
{
  if (isVisible)
  {
    // 每次打开时，都用父组件最新的 script 值重置编辑器
    scriptInEditor.value = props.script;
    // 重置全屏状态
    isModalFullscreen.value = false;
  }
});

// --- 拖拽调整大小逻辑 ---
const handleMouseDown = (e: MouseEvent) =>
{
  e.preventDefault();
  const startX = e.clientX;
  const startWidth = modalWidth.value;

  const handleMouseMove = (moveEvent: MouseEvent) =>
  {
    const newWidth = startWidth + (moveEvent.clientX - startX);
    modalWidth.value = Math.max(minWidth, newWidth);
  };

  const handleMouseUp = () =>
  {
    window.removeEventListener('mousemove', handleMouseMove);
    window.removeEventListener('mouseup', handleMouseUp);
    document.body.style.userSelect = '';
    document.body.style.cursor = '';
  };

  window.addEventListener('mousemove', handleMouseMove);
  window.addEventListener('mouseup', handleMouseUp);
  document.body.style.userSelect = 'none';
  document.body.style.cursor = 'ew-resize';
};

// --- 事件处理 ---
function handleReset()
{
  scriptInEditor.value = props.defaultScript;
}

function handleSave()
{
  // if (!editorScriptError.value)
  // {
  emit('update:script', scriptInEditor.value);
  emit('update:show', false);
  // }
}
</script>

<style scoped>
.resize-handle {
  position: absolute;
  right: -3px;
  top: 0;
  bottom: 0;
  width: 6px;
  cursor: ew-resize;
}
</style>