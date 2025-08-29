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
      <BackpackItemDisplay
          v-for="item in playerStore.ownedItems"
          :key="item.id"
          :item-id="item.id"
          context="backpack"
      />
    </n-list>
  </n-flex>
</template>

<script setup lang="ts">
import { NFlex, NH1, NList, NEmpty, NPageHeader } from 'naive-ui';
import { useBackpackStore } from './backpackStore.ts';
import BackpackItemDisplay from "#/features/backpack/BackpackItemDisplay.vue";

const playerStore = useBackpackStore();
</script>