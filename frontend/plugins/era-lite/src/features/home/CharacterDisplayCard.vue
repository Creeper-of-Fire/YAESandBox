<!-- era-lite/src/components/CharacterDisplayCard.vue -->
<template>
  <n-card :title="title">
    <n-empty v-if="!character" :description="`尚未选择${title}`">
      <template #extra>
        <n-button @click="router.push({ name: 'Era_Lite_Characters' })">去选择</n-button>
      </template>
    </n-empty>
    <n-flex v-else align="center">
      <CharacterAvatar :character="character" :size="60"/>
      <div>
        <n-h3 style="margin: 0">{{ character.name }}</n-h3>
        <p style="white-space: pre-wrap;">{{ character.description }}</p>
      </div>
    </n-flex>
  </n-card>
</template>

<script lang="ts" setup>
import {NButton, NCard, NEmpty, NFlex, NH3} from 'naive-ui';
import {type Character} from '#/types/models.ts';
import {useRouter} from "vue-router";
import CharacterAvatar from "#/components/CharacterAvatar.vue";

defineProps<{
  title: string;
  character: Character | undefined;
}>();

const router = useRouter();
</script>