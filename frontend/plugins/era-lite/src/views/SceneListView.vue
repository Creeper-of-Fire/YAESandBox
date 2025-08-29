<!-- era-lite/src/views/SceneListView.vue -->
<template>
  <n-flex vertical :size="24">
    <n-page-header>
      <template #title>
        <n-h1 style="margin:0">场景列表</n-h1>
      </template>
      <template #extra>
        <n-button type="primary" @click="openCreateModal">新建场景</n-button>
      </template>
    </n-page-header>

    <GeneratorPanel
        v-model="generatedScene"
        :schema="sceneSchema"
        storage-key="scene-generator"
        :expected-inputs="['topic']"
        title="AI 场景生成器"
        generation-prompt-label="输入你想要生成的场景描述，例如：一个悬浮在云海之上的古代图书馆"
        @accept="addGeneratedSceneToStore"
    />

    <n-list bordered hoverable clickable>
      <n-list-item v-for="scene in sceneStore.scenes" :key="scene.id">
        <n-thing :title="scene.name" :description="scene.description"/>
        <template #suffix>
          <n-flex>
            <n-button @click="sessionStore.setCurrentScene(scene.id)"
                      :type="sessionStore.currentSceneId === scene.id ? 'primary' : 'default'" ghost>选择此场景
            </n-button>
            <n-button @click="openEditModal(scene.id)" circle>
              <template #icon>
                <n-icon :component="EditIcon"/>
              </template>
            </n-button>
            <n-button @click="handleDelete(scene)" circle type="error" ghost>
              <template #icon>
                <n-icon :component="DeleteIcon"/>
              </template>
            </n-button>
          </n-flex>
        </template>
      </n-list-item>
    </n-list>
    <SceneEditor v-model:show="showEditorModal" :scene-id="editingSceneId"/>
  </n-flex>
</template>

<script setup lang="ts">
import {ref} from 'vue';
import {NButton, NFlex, NH1, NIcon, NList, NListItem, NPageHeader, NThing, useDialog, useMessage} from 'naive-ui';
import {useSceneStore} from '../stores/sceneStore';
import {useSessionStore} from '../stores/sessionStore';
import SceneEditor from '#/components/SceneEditor.vue';
import {Pencil as EditIcon, TrashBinOutline as DeleteIcon} from '@vicons/ionicons5';
import type {Scene} from '#/types/models';
import GeneratorPanel from "#/components/GeneratorPanel.vue";
import type {SchemaField} from "#/types/generator.ts";

const sceneStore = useSceneStore();
const sessionStore = useSessionStore();
const dialog = useDialog();
const message = useMessage();

const showEditorModal = ref(false);
const editingSceneId = ref<string | null>(null);

function openCreateModal()
{
  editingSceneId.value = null;
  showEditorModal.value = true;
}

function openEditModal(id: string)
{
  editingSceneId.value = id;
  showEditorModal.value = true;
}

function handleDelete(scene: Scene)
{
  dialog.warning({
    title: '确认删除',
    content: `你确定要删除场景 “${scene.name}” 吗？`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      sceneStore.deleteScene(scene.id);
      message.success('场景已删除');
    },
  });
}

// --- AI 生成器逻辑 ---
const sceneSchema: SchemaField[] = [
  { key: 'name', label: '场景名称', type: 'text' },
  { key: 'description', label: '场景描述', type: 'textarea' },
];
const generatedScene = ref<Partial<Scene> | null>(null);

function addGeneratedSceneToStore(newScene: Partial<Scene>) {
  if (!newScene.name) {
    message.error('AI 生成的数据不完整，已丢弃。');
    return;
  }
  sceneStore.addScene({
    name: newScene.name,
    description: newScene.description || '无描述',
  });
  message.success(`场景 “${newScene.name}” 已成功创建！`);
  generatedScene.value = null;
}
</script>