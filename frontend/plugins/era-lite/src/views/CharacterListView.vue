<!-- era-lite/src/views/CharacterListView.vue -->
<template>
  <n-flex vertical :size="24">
    <n-h1>角色列表</n-h1>
    <n-list bordered hoverable clickable>
      <n-list-item v-for="char in characterStore.characters" :key="char.id">
        <template #prefix>
          <n-avatar :size="48" style="font-size: 32px;">{{ char.avatar }}</n-avatar>
        </template>
        <n-thing :title="char.name" :description="char.description" />
        <template #suffix>
          <n-flex>
            <n-button
                @click="sessionStore.setProtagonist(char.id)"
                :type="sessionStore.protagonistId === char.id ? 'primary' : 'default'"
                ghost
            >
              设为主角
            </n-button>
            <n-button
                @click="sessionStore.setInteractTarget(char.id)"
                :type="sessionStore.interactTargetId === char.id ? 'success' : 'default'"
                ghost
            >
              设为交互对象
            </n-button>
          </n-flex>
        </template>
      </n-list-item>
    </n-list>
  </n-flex>
</template>

<script setup lang="ts">
import { NFlex, NH1, NList, NListItem, NAvatar, NThing, NButton } from 'naive-ui';
import { useCharacterStore } from '../stores/characterStore';
import { useSessionStore } from '../stores/sessionStore';

const characterStore = useCharacterStore();
const sessionStore = useSessionStore();
</script>