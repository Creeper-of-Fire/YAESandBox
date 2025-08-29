<!-- era-lite/src/views/ShopView.vue -->
<template>
  <n-flex :size="24" vertical>
    <n-page-header>
      <template #title>
        <n-h1 style="margin:0">道具商店</n-h1>
      </template>
      <template #extra>
        <n-flex align="center">
          <span>你的金钱: {{ backpackStore.money }} G</span>
          <n-button @click="openCreateModal" type="primary">手动添加商品</n-button>
          <n-button :loading="shopStore.isLoading" @click="shopStore.generateShopItems()">
            刷新商品
          </n-button>
        </n-flex>
      </template>
    </n-page-header>

    <GeneratorPanel
        v-model="generatedItem"
        :schema="itemSchema"
        storage-key="shop-item-generator"
        :expected-inputs="['topic']"
        title="AI 商品生成器"
        generation-prompt-label="输入你想要生成的物品描述，例如：一把能斩断噩梦的短剑"
        @accept="addGeneratedItemToShop"
    />

    <n-list bordered hoverable>
      <ShopItemDisplay
          v-for="item in shopStore.itemsForSale"
          :key="item.id"
          :item-id="item.id"
      />
    </n-list>

    <ItemEditor
        v-model:show="showCreateModal"
        mode="create"
        :initial-data="null"
        @save="handleCreate"
    />
  </n-flex>
</template>

<script lang="ts" setup>
import {NButton, NFlex, NH1, NList, NPageHeader, useMessage} from 'naive-ui';
import {useShopStore} from '../stores/shopStore';
import {useBackpackStore} from '../stores/backpackStore.ts';
import {ref} from "vue";
import type {SchemaField} from '#/types/generator.ts';
import type {Item} from '#/types/models.ts';
import ShopItemDisplay from "#/components/ShopItemDisplay.vue";
import ItemEditor from "#/components/ItemEditor.vue";
import GeneratorPanel from "#/components/GeneratorPanel.vue";

const shopStore = useShopStore();
const backpackStore = useBackpackStore();
const message = useMessage();

// --- 编辑器模态框状态 ---
const showCreateModal = ref(false);
const editingItemId = ref<string | null>(null);

function openCreateModal()
{
  editingItemId.value = null;
  showCreateModal.value = true;
}

function handleCreate(newItemData: Omit<Item, 'id'>) {
  shopStore.addItem(newItemData);
  message.success('新物品已创建');
}

// --- AI 生成逻辑 ---

// 1. 定义 Item 的 Schema
const itemSchema: SchemaField[] = [
  {key: 'name', label: '物品名称', type: 'text'},
  {key: 'description', label: '物品描述', type: 'textarea'},
  {key: 'price', label: '价格', type: 'number'},
];

// 2. 创建一个 ref 来接收生成的数据
const generatedItem = ref<Partial<Item> | null>(null);

// 3. 实现 accept 事件的回调
function addGeneratedItemToShop(newItem: Partial<Item>)
{
  // 进行数据校验，确保核心字段存在
  if (!newItem.name || !newItem.price)
  {
    message.error('AI生成的数据不完整，已丢弃。');
    return;
  }

  shopStore.addItem({
    name: newItem.name,
    description: newItem.description || '无描述',
    price: newItem.price,
  });
  message.success(`“${newItem.name}”已成功添加到商店！`);
  generatedItem.value = null; // 清空，准备下一次生成
}
</script>