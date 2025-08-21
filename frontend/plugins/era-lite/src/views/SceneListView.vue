<!-- era-lite/src/views/SceneListView.vue -->
<template>
  <n-flex vertical :size="24">
    <n-h1>场景列表</n-h1>
    <n-list bordered hoverable clickable>
      <n-list-item v-for="scene in sceneStore.scenes" :key="scene.id">
        <n-thing :title="scene.name" :description="scene.description" />
        <template #suffix>
          <n-button
              @click="sessionStore.setCurrentScene(scene.id)"
              :type="sessionStore.currentSceneId === scene.id ? 'primary' : 'default'"
              ghost
          >
            选择此场景
          </n-button>
        </template>
      </n-list-item>
    </n-list>
  </n-flex>
</template>

<script setup lang="ts">
import { NFlex, NH1, NList, NListItem, NThing, NButton } from 'naive-ui';
import { useSceneStore } from '../stores/sceneStore';
import { useSessionStore } from '../stores/sessionStore';

const sceneStore = useSceneStore();
const sessionStore = useSessionStore();
</script>