<!-- era-lite/src/components/SceneItemDisplay.vue -->
<template>
  <n-list-item v-if="scene">
    <n-thing
        :content-style="{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }"
        :description="scene.description"
        :description-style="{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }"
        :title="scene.name"
    />
    <template #suffix>
      <n-flex>
        <n-button :type="sessionStore.currentSceneId === scene.id ? 'primary' : 'default'"
                  ghost @click="sessionStore.setCurrentScene(scene.id)">选择此场景
        </n-button>
        <n-button circle @click="openEditModal">
          <template #icon>
            <n-icon :component="EditIcon"/>
          </template>
        </n-button>
        <n-button circle ghost type="error" @click="handleDelete">
          <template #icon>
            <n-icon :component="DeleteIcon"/>
          </template>
        </n-button>
      </n-flex>
    </template>
  </n-list-item>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NButton, NFlex, NIcon, NListItem, NThing, useDialog, useMessage} from 'naive-ui';
import {useSceneStore} from '#/features/scenes/sceneStore.ts';
import {useSessionStore} from '#/stores/sessionStore.ts';
import {Pencil as EditIcon, TrashBinOutline as DeleteIcon} from '@vicons/ionicons5';
import type {Scene} from '#/types/models.ts';
import {sceneSchema} from "#/schemas/entitySchemas.ts";
import {useEntityEditorModal} from "#/components/useEntityEditorModal.tsx";

const props = defineProps<{ sceneId: string }>();

const sceneStore = useSceneStore();
const sessionStore = useSessionStore();
const dialog = useDialog();
const message = useMessage();
const entityEditor = useEntityEditorModal<Scene>();

const scene = computed(() => sceneStore.scenes.find(s => s.id === props.sceneId));

function openEditModal()
{
  if (!scene.value) return;

  entityEditor.open({
    mode: 'edit',
    entityName: '场景',
    schema: sceneSchema,
    initialData: scene.value,
    onSave: (updatedScene) =>
    {
      sceneStore.updateScene(updatedScene);
      message.success('场景信息已更新');
    },
  });
}

function handleDelete()
{
  if (!scene.value) return;
  dialog.warning({
    title: '确认删除',
    content: `你确定要删除场景 “${scene.value.name}” 吗？`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      sceneStore.deleteScene(props.sceneId);
      message.success('场景已删除');
    },
  });
}
</script>