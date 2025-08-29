<!-- era-lite/src/components/CharacterDisplayCard.vue -->
<template>
  <n-card :title="title">
    <n-empty v-if="!character" :description="`尚未选择${title}`">
      <template #extra>
        <n-button @click="router.push({ name: 'Era_Lite_Characters' })">去选择</n-button>
      </template>
    </n-empty>
    <n-flex v-else align="center">
      <n-avatar :size="60" style="font-size: 40px;">{{ character.avatar }}</n-avatar>
      <div>
        <n-h3 style="margin: 0">{{ character.name }}</n-h3>
        <p style="white-space: pre-wrap;">{{ character.description }}</p>
      </div>
    </n-flex>
  </n-card>
</template>

<script lang="ts" setup>
import {NAvatar, NButton, NCard, NEmpty, NH3, NFlex} from 'naive-ui';
import {type Character} from '#/types/models.ts';
import {useRouter} from "vue-router";

defineProps<{
  title: string;
  character: Character | undefined;
}>();

const router = useRouter();
</script>