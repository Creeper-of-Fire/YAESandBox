<!-- era-lite/src/views/BackpackView.vue -->
<template>
  <n-flex vertical :size="24">
    <n-page-header>
      <template #title><n-h1 style="margin:0">我的背包</n-h1></template>
      <template #extra>
        <span>金钱: {{ playerStore.money }} G</span>
      </template>
    </n-page-header>
    <n-empty v-if="playerStore.ownedItems.length === 0" description="你的背包空空如也" />
    <n-list v-else bordered hoverable>
      <n-list-item v-for="item in playerStore.ownedItems" :key="item.id">
        <n-thing :title="item.name" :description="item.description" />
      </n-list-item>
    </n-list>
  </n-flex>
</template>

<script setup lang="ts">
import { NFlex, NH1, NList, NListItem, NThing, NEmpty, NPageHeader } from 'naive-ui';
import { usePlayerStore } from '../stores/playerStore';

const playerStore = usePlayerStore();
</script>