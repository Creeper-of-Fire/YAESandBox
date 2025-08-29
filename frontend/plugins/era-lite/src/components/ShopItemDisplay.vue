<!-- era-lite/src/components/ShopItemDisplay.vue -->
<template>
  <BaseItemDisplay v-if="item" :description="item.description" :title="`${item.name} - ${item.price} G`">
    <template #actions>
      <n-button :disabled="backpackStore.money < item.price" @click="handleBuy">
        购买
      </n-button>
      <n-button @click="openEditModal">编辑</n-button>
      <n-button ghost type="error" @click="handleDelete">删除</n-button>
    </template>
  </BaseItemDisplay>

  <EntityEditor
      v-if="item"
      v-model:show="showEditorModal"
      :initial-data="item"
      :schema="itemSchema"
      entity-name="物品"
      mode="edit"
      @save="handleSave"
  />
</template>

<script lang="ts" setup>
import {computed, ref} from 'vue';
import {NButton, useDialog, useMessage} from 'naive-ui';
import {useShopStore} from '#/stores/shopStore';
import {useBackpackStore} from '#/stores/backpackStore';
import BaseItemDisplay from './BaseItemDisplay.vue';
import EntityEditor from "#/components/EntityEditor.vue";
import type {Item} from '#/types/models';
import {itemSchema} from "#/schemas/entitySchemas.ts";

const props = defineProps<{ itemId: string }>();

const shopStore = useShopStore();
const backpackStore = useBackpackStore();
const message = useMessage();
const dialog = useDialog();

const item = computed(() => shopStore.itemsForSale.find(i => i.id === props.itemId));

const showEditorModal = ref(false);

function openEditModal()
{
  showEditorModal.value = true;
}

function handleSave(updatedItem: Item)
{
  shopStore.updateItem(updatedItem);
  message.success('物品已更新');
}

function handleBuy()
{
  if (!item.value) return;
  if (backpackStore.spendMoney(item.value.price))
  {
    backpackStore.addItem(item.value); // 注意：这里传递的是 item 的数据，而不是实例
    message.success(`成功购买 ${item.value.name}!`);
  }
  else
  {
    message.error('金钱不足！');
  }
}

function handleDelete()
{
  if (!item.value) return;
  dialog.warning({
    title: '确认删除',
    content: `你确定要从商店中永久删除 “${item.value.name}” 吗？`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      shopStore.deleteItem(props.itemId);
      message.success(`“${item.value?.name}” 已被删除`);
    },
  });
}
</script>