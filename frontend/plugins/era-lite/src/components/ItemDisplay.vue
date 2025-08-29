<!-- era-lite/src/components/ItemDisplay.vue -->
<template>
  <n-list-item v-if="item">
    <n-thing
        :title="`${item.name} ${context === 'shop' ? '- ' + item.price + ' G' : ''}`"
        :description="item.description"
        :content-style="{ whiteSpace: 'normal', wordBreak: 'break-word' }"
        :header-style="{ whiteSpace: 'normal', wordBreak: 'break-word' }"
        style="white-space: pre-wrap;"
    />
    <template #suffix>
      <!-- 商店上下文的操作 -->
      <n-flex v-if="context === 'shop'">
        <n-button :disabled="playerStore.money < item.price" @click="handleBuy">
          购买
        </n-button>
        <n-button @click="$emit('edit', item!.id)">编辑</n-button>
        <n-button type="error" ghost @click="handleDelete">删除</n-button>
      </n-flex>
      <!-- 背包上下文的操作 -->
      <n-flex v-if="context === 'backpack'">
        <n-button type="warning" ghost @click="handleDrop">丢弃</n-button>
      </n-flex>
    </template>
  </n-list-item>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { NListItem, NThing, NFlex, NButton, useMessage, useDialog } from 'naive-ui';
import { useShopStore } from '#/stores/shopStore';
import { usePlayerStore } from '#/stores/playerStore';
import type { Item } from '#/types/models';

const props = defineProps<{
  itemId: string;
  context: 'shop' | 'backpack';
}>();

const emit = defineEmits<{
  (e: 'edit', id: string): void;
}>();

const shopStore = useShopStore();
const playerStore = usePlayerStore();
const message = useMessage();
const dialog = useDialog();

// 组件的核心：根据ID和上下文，从对应的store中查找数据
const item = computed<Item | undefined>(() => {
  if (props.context === 'shop') {
    return shopStore.itemsForSale.find(i => i.id === props.itemId);
  }
  if (props.context === 'backpack') {
    return playerStore.ownedItems.find(i => i.id === props.itemId);
  }
  return undefined;
});

function handleBuy() {
  if (!item.value) return;
  if (playerStore.spendMoney(item.value.price)) {
    playerStore.addItem(item.value);
    message.success(`成功购买 ${item.value.name}!`);
  } else {
    message.error('金钱不足！');
  }
}

function handleDelete() {
  if (!item.value) return;
  dialog.warning({
    title: '确认删除',
    content: `你确定要从商店中永久删除 “${item.value.name}” 吗？`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () => {
      shopStore.deleteItem(props.itemId);
      message.success(`“${item.value?.name}” 已被删除`);
    },
  });
}

function handleDrop() {
  if (!item.value) return;
  dialog.warning({
    title: '确认丢弃',
    content: `你确定要丢弃 “${item.value.name}” 吗？`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () => {
      playerStore.removeItem(props.itemId);
      message.success(`已丢弃 “${item.value?.name}”`);
    },
  });
}
</script>