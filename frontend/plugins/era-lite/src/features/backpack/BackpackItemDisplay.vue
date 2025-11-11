<!-- era-lite/src/components/BackpackItemDisplay.vue -->
<template>
  <BaseItemDisplay v-if="item" :description="item.description" :title="item.name">
    <template #actions>
      <n-button @click="openEditModal">编辑</n-button>
      <n-button ghost type="warning" @click="handleDrop">丢弃</n-button>
    </template>
  </BaseItemDisplay>
</template>

<script lang="ts" setup>
import {computed} from 'vue';
import {NButton, useDialog, useMessage} from 'naive-ui';
import {useBackpackStore} from '#/features/backpack/backpackStore.ts';
import BaseItemDisplay from '../../components/BaseItemDisplay.vue';
import type {Item} from '#/types/models.ts';
import {itemSchema} from "#/schemas/entitySchemas.ts";
import {useEntityEditorModal} from "#/components/useEntityEditorModal.tsx";

const props = defineProps<{ itemId: string }>();
const backpackStore = useBackpackStore();
const dialog = useDialog();
const message = useMessage();

const item = computed(() => backpackStore.ownedItems.find(i => i.id === props.itemId));

const entityEditor = useEntityEditorModal<Item>();

function openEditModal()
{
  if (!item.value) return;
  entityEditor.open({
    mode: 'edit',
    entityName: '背包物品',
    schema: itemSchema,
    initialData: item.value,
    onSave: updatedItem =>
    {
      backpackStore.updateItemInBackpack(updatedItem);
      message.success('背包中的物品已更新');
    }
  });
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