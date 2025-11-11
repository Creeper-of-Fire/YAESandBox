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
          <n-button type="primary" @click="openCreateModal">手动添加商品</n-button>
          <n-button :loading="shopStore.isLoading" @click="shopStore.generateShopItems()">
            刷新商品
          </n-button>
        </n-flex>
      </template>
    </n-page-header>

    <GeneratorPanel
        v-model="generatedItem"
        :expected-inputs="['topic']"
        :required-tags="['物品生成']"
        :schema="itemSchema"
        entity-name="物品"
        generation-prompt-label="输入你想要生成的物品描述，例如：一把能斩断噩梦的短剑"
        storage-key="shop-item-generator"
        title="AI 商品生成器"
        @accept="addGeneratedItemToShop"/>

    <n-list bordered hoverable>
      <ShopItemDisplay
          v-for="item in shopStore.itemsForSale"
          :key="item.id"
          :item-id="item.id"
      />
    </n-list>
  </n-flex>
</template>

<script lang="ts" setup>
import {NButton, NFlex, NH1, NList, NPageHeader, useMessage} from 'naive-ui';
import {useShopStore} from './shopStore.ts';
import {useBackpackStore} from '../backpack/backpackStore.ts';
import {ref} from "vue";
import type {Item} from '#/types/models.ts';
import ShopItemDisplay from "#/features/shop/ShopItemDisplay.vue";
import GeneratorPanel from "#/components/GeneratorPanel.vue";
import {itemSchema} from "#/schemas/entitySchemas.ts";
import {useEntityEditorModal} from "#/components/useEntityEditorModal.tsx";

const shopStore = useShopStore();
const backpackStore = useBackpackStore();
const message = useMessage();

// --- 编辑器模态框状态 ---
const entityEditor = useEntityEditorModal<Item>();

function openCreateModal()
{
  entityEditor.open({
    mode: 'create',
    entityName: '物品',
    schema: itemSchema,
    initialData: null,
    onSave: (itemData) =>
    {
      shopStore.addItem(itemData);
      message.success('新物品已创建');
    }
  });
}

// --- AI 生成逻辑 ---
const generatedItem = ref<Partial<Item> | null>(null);

// "直接添加" 按钮的回调
function addGeneratedItemToShop(itemData: Omit<Item, 'id'>)
{
  shopStore.addItem(itemData);
  message.success(`“${itemData.name}”已成功添加到商店！`);
  generatedItem.value = null;
}
</script>