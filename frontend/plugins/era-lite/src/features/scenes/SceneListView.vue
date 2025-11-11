<!-- era-lite/src/views/SceneListView.vue -->
<template>
  <n-flex :size="24" vertical>
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
        :expected-inputs="['topic']"
        :schema="sceneSchema"
        entity-name="场景"
        :required-tags="['场景生成']"
        generation-prompt-label="输入你想要生成的场景描述，例如：一个悬浮在云海之上的古代图书馆"
        storage-key="scene-generator"
        title="AI 场景生成器"
        @accept="addGeneratedSceneToStore"/>

    <n-list bordered clickable hoverable>
      <SceneItemDisplay
          v-for="scene in sceneStore.scenes"
          :key="scene.id"
          :scene-id="scene.id"
      />
    </n-list>
  </n-flex>
</template>

<script lang="ts" setup>
import {ref} from 'vue';
import {NButton, NFlex, NH1, NList, NPageHeader, useMessage} from 'naive-ui';
import {useSceneStore} from './sceneStore.ts';
import type {Scene} from '#/types/models.ts';
import GeneratorPanel from "#/components/GeneratorPanel.vue";
import SceneItemDisplay from "#/features/scenes/SceneItemDisplay.vue";
import {sceneSchema} from "#/schemas/entitySchemas.ts";
import {useEntityEditorModal} from "#/components/useEntityEditorModal.tsx";

const sceneStore = useSceneStore();
const message = useMessage();
const entityEditor = useEntityEditorModal<Scene>();

// --- 场景模态框逻辑 ---
function openCreateModal() {
  entityEditor.open({
    mode: 'create',
    entityName: '场景',
    schema: sceneSchema,
    initialData: null,
    onSave: (sceneData) => {
      sceneStore.addScene(sceneData);
      message.success('新场景已创建');
    }
  });
}

// --- AI 生成器逻辑 ---
const generatedScene = ref<Partial<Scene> | null>(null);

function addGeneratedSceneToStore(newScene: Omit<Scene, 'id'>)
{
  sceneStore.addScene(newScene);
  message.success(`场景 “${newScene.name}” 已成功创建！`);
  generatedScene.value = null;
}
</script>