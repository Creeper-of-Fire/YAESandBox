<!-- era-lite/src/views/ShopView.vue -->
<template>
  <n-flex :size="24" vertical>
    <n-page-header>
      <template #title>
        <n-h1 style="margin:0">道具商店</n-h1>
      </template>
      <template #extra>
        <n-flex align="center">
          <span>你的金钱: {{ playerStore.money }} G</span>
          <n-button @click="openCreateModal" type="primary">手动添加商品</n-button>
          <n-button :loading="shopStore.isLoading" @click="shopStore.generateShopItems()">
            刷新商品
          </n-button>
        </n-flex>
      </template>
    </n-page-header>

    <n-card title="AI 商品生成器">
      <n-flex :size="12" vertical>
        <n-input
            v-model:value="generationTopic"
            clearable
            placeholder="输入你想要生成的物品描述，例如：一把能斩断噩梦的短剑"
            type="text"
        />
        <WorkflowProviderButton
            storage-key="shop-item-generator"
            :expected-inputs="['topic']"
            @click="handleGenerateItem"
        />
      </n-flex>
      <div v-if="isGenerating || generatedItem" style="margin-top: 24px;">
        <n-divider/>
        <n-alert v-if="generationError" title="生成失败" type="error">
          {{ generationError.message }}
        </n-alert>
        <div v-else-if="generatedItem">
          <n-thing>
            <template #header>
              <n-h3 style="margin: 0">
                {{ getText(generatedItem.name) || '...' }} - {{ parseNumber(generatedItem.price) || '...' }} G
              </n-h3>
            </template>
            <template #description>
              <p style="white-space: pre-wrap;">{{ getText(generatedItem.description) || '...' }}</p>
            </template>
          </n-thing>
          <n-collapse-transition :show="!!thinkingProcess">
            <n-log :log="thinkingProcess" language="text" trim/>
          </n-collapse-transition>
        </div>
        <n-flex v-if="isGenerationFinished" justify="end" style="margin-top: 16px;">
          <n-button type="primary" @click="addGeneratedItemToShop">添加到商店</n-button>
          <n-button @click="clearGeneratedItem">丢弃</n-button>
        </n-flex>
      </div>
    </n-card>

    <n-list bordered hoverable>
      <ItemDisplay
          v-for="item in shopStore.itemsForSale"
          :key="item.id"
          :item-id="item.id"
          context="shop"
          @edit="openEditModal"
      />
    </n-list>

    <!-- 编辑/创建模态框 -->
    <ItemEditor v-model:show="showEditorModal" :item-id="editingItemId"/>
  </n-flex>
</template>

<script lang="ts" setup>
import {NButton, NFlex, NH1, NList, NPageHeader, NThing, useMessage} from 'naive-ui';
import {useShopStore} from '../stores/shopStore';
import {usePlayerStore} from '../stores/playerStore';
import {useWorkflowStream} from '@yaesandbox-frontend/core-services/composables';
import WorkflowProviderButton from "#/components/WorkflowProviderButton.vue";
import type {WorkflowConfig} from "@yaesandbox-frontend/core-services/types";
import {computed, ref} from "vue";
import {getText, getThink, parseNumber} from "#/utils/workflowParser.ts";
import type {StreamedItem} from "#/types/streaming.ts";
import ItemDisplay from "#/components/ItemDisplay.vue";
import ItemEditor from "#/components/ItemEditor.vue";

const shopStore = useShopStore();
const playerStore = usePlayerStore();
const message = useMessage();

// --- 编辑器模态框状态 ---
const showEditorModal = ref(false);
const editingItemId = ref<string | null>(null);

function openCreateModal()
{
  editingItemId.value = null;
  showEditorModal.value = true;
}

function openEditModal(id: string)
{
  editingItemId.value = id;
  showEditorModal.value = true;
}

// --- AI 生成逻辑 ---
const generationTopic = ref<string>('');
const {
  data: generatedItem,
  isLoading: isGenerating,
  error: generationError,
  isFinished: isGenerationFinished,
  execute: executeItemGeneration,
} = useWorkflowStream<Partial<StreamedItem>>();

const thinkingProcess = computed(() =>
{
  if (!generatedItem.value) return '';
  const thoughts = [
    getThink(generatedItem.value.name),
    getThink(generatedItem.value.description),
    getThink(generatedItem.value.price)
  ].filter(Boolean);
  return thoughts.join('\n');
});

async function handleGenerateItem(config: WorkflowConfig)
{
  if (!generationTopic.value.trim())
  {
    message.warning('请输入物品的描述！');
    return;
  }
  const inputs = {topic: generationTopic.value};
  await executeItemGeneration(config, inputs);
}

function addGeneratedItemToShop()
{
  if (generatedItem.value)
  {
    const newItemData = {
      name: getText(generatedItem.value.name) || '未命名商品',
      description: getText(generatedItem.value.description) || '无描述',
      price: parseNumber(generatedItem.value.price) || 100,
    };
    // 使用 store action 来添加
    shopStore.addItem(newItemData);
    message.success(`“${newItemData.name}”已成功添加到商店！`);
    clearGeneratedItem();
  }
}

function clearGeneratedItem()
{
  generatedItem.value = null;
}
</script>