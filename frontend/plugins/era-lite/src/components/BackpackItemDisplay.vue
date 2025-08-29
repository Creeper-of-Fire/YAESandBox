<!-- era-lite/src/components/BackpackItemDisplay.vue -->
<template>
  <BaseItemDisplay v-if="item" :title="item.name" :description="item.description">
    <template #actions>
      <n-button @click="openEditModal">编辑</n-button>
      <n-button type="warning" ghost @click="handleDrop">丢弃</n-button>
    </template>
  </BaseItemDisplay>
  <ItemEditor
      v-if="item"
      v-model:show="showEditorModal"
      mode="edit"
      :initial-data="item"
      @save="handleSave"
  />
</template>

<script setup lang="ts">
import {computed, ref} from 'vue';
import {NButton, useDialog, useMessage} from 'naive-ui';
import {useBackpackStore} from '#/stores/backpackStore';
import BaseItemDisplay from './BaseItemDisplay.vue';
import ItemEditor from "#/components/ItemEditor.vue";
import type {Item} from '#/types/models';

const props = defineProps<{ itemId: string }>();
const backpackStore = useBackpackStore();
const dialog = useDialog();
const message = useMessage();

const item = computed(() => backpackStore.ownedItems.find(i => i.id === props.itemId));

const showEditorModal = ref(false);

function openEditModal()
{
  showEditorModal.value = true;
}

function handleSave(updatedItem: Item)
{
  backpackStore.updateItemInBackpack(updatedItem);
  message.success('背包中的物品已更新');
}

function handleDrop()
{
  if (!item.value) return;
  dialog.warning({
    title: '确认丢弃',
    content: `你确定要丢弃 “${item.value.name}” 吗？`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      backpackStore.removeItem(props.itemId);
      message.success(`已丢弃 “${item.value?.name}”`);
    },
  });
}
</script>