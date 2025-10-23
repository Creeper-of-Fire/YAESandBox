<template>
  <div class="editor-layout">
    <!-- 左侧：组件列表 -->
    <n-card class="components-pane" title="已注册组件">
      <template #header-extra>
        <n-button size="small" type="primary" @click="handleNewComponent">
          <template #icon>
            <n-icon :component="AddIcon"/>
          </template>
          新建组件
        </n-button>
      </template>

      <n-spin :show="!isStoreReady">
        <n-list clickable hoverable>
          <n-list-item v-for="comp in allComponents" :key="comp.id" @click="loadComponentIntoEditor(comp)">
            <n-thing :title="comp.id">
              <template #description>
                <n-tag :type="statusType(comp.status)" size="small">{{ comp.status }}</n-tag>
              </template>
              <template #header-extra>
                <n-button text type="error" @click.stop="handleDelete(comp.id)">
                  <template #icon>
                    <n-icon :component="TrashIcon"/>
                  </template>
                </n-button>
              </template>
            </n-thing>
          </n-list-item>
          <n-empty v-if="isStoreReady && allComponents.length === 0" description="暂无组件"/>
        </n-list>
      </n-spin>
    </n-card>
    <!-- 中间：代码编辑区 -->
    <n-card class="editor-pane" title="JSX Component Editor">
      <n-flex align="center" style="margin-bottom: 16px;">
        <label>语言：</label>
        <n-radio-group v-model:value="monacoLanguage" name="language-mode">
          <n-radio-button value="typescript">TypeScript (TSX)</n-radio-button>
          <n-radio-button value="javascript">JavaScript (JSX)</n-radio-button>
        </n-radio-group>
      </n-flex>

      <n-input
          v-model:value="componentId"
          placeholder="Component Tag Name (e.g., my-button)"
          style="margin-bottom: 16px;"
      />
      <SmartEditor
          v-model:model-value="sourceCode"
          :enhancer="jsxEnhancer"
          :key="monacoLanguage"
          :extra-libs="editorExtraLibs"
          :is-jsx="true"
          :language="monacoLanguage"
          height="calc(100vh - 400px)"
          storage-key="jsx-editor-mode"
      />
      <template #footer>
        <n-button :loading="isCompiling" type="primary" @click="handleCompile">
          Compile & Register
        </n-button>
        <n-alert v-if="errorMessage" style="margin-top: 16px;" type="error">
          {{ errorMessage }}
        </n-alert>
      </template>
    </n-card>

    <!-- 右侧：实时预览区 -->
    <n-card class="preview-pane" title="Live Preview & Test Case">
      <template #header-extra>
        <n-tag type="info">Status: {{ componentState?.status || 'idle' }}</n-tag>
      </template>

      <!-- 测试用例文本域 -->
      <n-h6>Test Content for ContentRenderer</n-h6>
      <n-input
          v-model:value="testContent"
          :rows="8"
          placeholder="Write the content to be rendered..."
          style="font-family: monospace; margin-bottom: 16px;"
          type="textarea"
      />

      <n-h6>Live Preview</n-h6>
      <div v-if="componentState?.status === 'ready'">
        <div class="preview-box">
          <ContentRenderer :content="testContent"/>
        </div>
      </div>
      <div v-else>
        <p>Compile the component to see a preview.</p>
      </div>
    </n-card>
  </div>
</template>

<script lang="ts" setup>
import {computed, ref} from 'vue';
import {type DynamicComponentState, useComponentStore} from '../stores/useComponentStore';
import {NAlert, NButton, NCard, NInput, useDialog} from 'naive-ui';
import {ContentRenderer} from '@yaesandbox-frontend/shared-ui/content-renderer';
import {exampleCode, exampleContent, exampleName} from "#/views/example.ts";
import {AddIcon, TrashIcon} from "@yaesandbox-frontend/shared-ui/icons";
import {useCodeSafetyCheck, useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import {JsxTsLanguageEnhancer, SmartEditor} from "@yaesandbox-frontend/shared-feature/monaco-editor"
import {injectionDtsContent, injectionDtsPath} from '../monaco-injection-types';

const editorExtraLibs = [
  {
    content: injectionDtsContent,
    filePath: injectionDtsPath
  }
];
const dialog = useDialog();
const store = useComponentStore();
const isStoreReady = computed(() => store.isReady);

const monacoLanguage = useScopedStorage<'typescript' | 'javascript'>('jsx-monaco-editor-language','typescript');
const jsxEnhancer = computed(() => new JsxTsLanguageEnhancer(monacoLanguage.value));

// 编辑器状态
const testContent = ref('');
const componentId = ref('');
const sourceCode = ref('');

// 从 store 派生计算属性
const allComponents = computed(() => Array.from(store.components.values()));
const componentState = computed(() => store.components.get(componentId.value.toLowerCase()));
const isCompiling = computed(() => componentState.value?.status === 'compiling');
const errorMessage = computed(() => componentState.value?.error);

// 事件处理
const {checkCodeSafety} = useCodeSafetyCheck();
const handleCompile = async () =>
{
  if (!componentId.value || !sourceCode.value)
  {
    alert('Component tag name and source code cannot be empty.');
    return;
  }

  // 2. 在执行任何操作前，调用安全检查
  const canProceed = await checkCodeSafety(sourceCode.value);

  // 3. 如果用户取消，则直接返回
  if (!canProceed)
  {
    console.log('用户取消了编译操作。');
    return;
  }

  // 4. 如果用户同意，则继续执行原有的逻辑
  store.addOrUpdateComponent(componentId.value, sourceCode.value, testContent.value);
};


const loadComponentIntoEditor = (comp: DynamicComponentState) =>
{
  componentId.value = comp.id;
  sourceCode.value = comp.source;
  testContent.value = comp.testContent;
};

const handleNewComponent = () =>
{
  componentId.value = exampleName;
  sourceCode.value = exampleCode;
  testContent.value = exampleContent;
};

const handleDelete = (id: string) =>
{
  dialog.warning({
    title: '确认删除',
    content: `你确定要永久删除组件 "${id}" 吗？`,
    positiveText: '删除',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      store.deleteComponent(id);
      if (componentId.value.toLowerCase() === id.toLowerCase())
      {
        componentId.value = '';
        sourceCode.value = '';
      }
    },
  });
};
const statusType = (status: string) =>
{
  if (status === 'ready') return 'success';
  if (status === 'error') return 'error';
  return 'default';
};
</script>

<style scoped>
.editor-layout {
  display: flex;
  gap: 16px;
  padding: 16px;
}

.components-pane {
  flex: 0 0 300px;
}

.editor-pane {
  flex: 2;
}

.preview-pane {
  flex: 1;
}

.preview-box {
  border: 1px dashed #ccc;
  padding: 16px;
  border-radius: 4px;
  min-height: 200px;
}
</style>