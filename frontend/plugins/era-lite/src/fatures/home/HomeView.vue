<!-- era-lite/src/views/HomeView.vue -->
<template>
  <n-flex vertical :size="24">
    <n-h1>欢迎来到 Era-Lite</n-h1>

    <!-- 当前选择 -->
    <n-grid :cols="2" :x-gap="24">
      <n-gi>
        <character-display-card title="主角" :character="sessionStore.selectedProtagonist" />
      </n-gi>
      <n-gi>
        <character-display-card title="交互对象" :character="sessionStore.selectedInteractTarget" />
      </n-gi>
    </n-grid>

    <n-card title="当前场景">
      <n-empty v-if="!sessionStore.selectedScene" description="尚未选择场景">
        <template #extra>
          <n-button @click="router.push({ name: 'Scenes' })">去选择</n-button>
        </template>
      </n-empty>
      <div v-else>
        <n-h3 style="margin: 0">{{ sessionStore.selectedScene.name }}</n-h3>
        <p>{{ sessionStore.selectedScene.description }}</p>
      </div>
    </n-card>

    <!-- 操作按钮 -->
    <n-card title="开始行动">
      <n-flex>
        <n-button size="large" type="primary" :disabled="!sessionStore.isReadyForInteraction">
          开始交互 (WIP)
        </n-button>
        <n-button size="large" @click="sessionStore.clearSelections()" ghost>
          清空选择
        </n-button>
      </n-flex>
    </n-card>
  </n-flex>
</template>

<script setup lang="ts">
import { NFlex, NH1, NGrid, NGi, NCard, NEmpty, NButton, NH3 } from 'naive-ui';
import { useSessionStore } from '../../stores/sessionStore.ts';
import CharacterDisplayCard from './CharacterDisplayCard.vue';
import {useRouter} from "vue-router";

const router = useRouter();
const sessionStore = useSessionStore();
</script>