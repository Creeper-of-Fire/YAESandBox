<!-- era-lite/src/views/ShopView.vue -->
<template>
  <n-flex vertical :size="24">
    <n-page-header>
      <template #title><n-h1 style="margin:0">道具商店</n-h1></template>
      <template #extra>
        <n-flex align="center">
          <span>你的金钱: {{ playerStore.money }} G</span>
          <n-button @click="shopStore.generateShopItems()" :loading="shopStore.isLoading">
            刷新商品
          </n-button>
        </n-flex>
      </template>
    </n-page-header>
    <n-list bordered hoverable>
      <n-list-item v-for="item in shopStore.itemsForSale" :key="item.id">
        <n-thing :title="`${item.name} - ${item.price} G`" :description="item.description" />
        <template #suffix>
          <n-button @click="buyItem(item)" :disabled="playerStore.money < item.price">
            购买
          </n-button>
        </template>
      </n-list-item>
    </n-list>
  </n-flex>
</template>

<script setup lang="ts">
import { NFlex, NH1, NList, NListItem, NThing, NButton, NPageHeader, useMessage } from 'naive-ui';
import { useShopStore } from '../stores/shopStore';
import { usePlayerStore } from '../stores/playerStore';
import { type Item } from '#/types/models';

const shopStore = useShopStore();
const playerStore = usePlayerStore();
const message = useMessage();

function buyItem(item: Item) {
  if (playerStore.spendMoney(item.price)) {
    playerStore.addItem(item);
    message.success(`成功购买 ${item.name}!`);
  } else {
    message.error('金钱不足！');
  }
}
</script>