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
          <n-button :loading="shopStore.isLoading" @click="shopStore.generateShopItems()">
            刷新商品
          </n-button>
        </n-flex>
      </template>
    </n-page-header>

    <n-card title="AI 商品生成器">
      <n-flex :size="12" vertical>
        <!-- 用户输入区域 -->
        <n-input
            v-model:value="generationTopic"
            clearable
            placeholder="输入你想要生成的物品描述，例如：一把能斩断噩梦的短剑"
            type="text"
        />

        <!-- 我们的配置提供者按钮 -->
        <WorkflowProviderButton
            storage-key="shop-item-generator"
            @click="handleGenerateItem"
        />
      </n-flex>

      <!-- AI生成结果展示区域-->
      <div v-if="isGenerating || generatedItem" style="margin-top: 24px;">
        <n-divider/>
        <n-alert v-if="generationError" title="生成失败" type="error">
          {{ generationError.message }}
        </n-alert>
        <div v-else-if="generatedItem">
          <!-- 主要内容展示 -->
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

          <!-- AI 思考过程展示 -->
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
      <n-list-item v-for="item in shopStore.itemsForSale" :key="item.id">
        <n-thing :description="item.description" :title="`${item.name} - ${item.price} G`"/>
        <template #suffix>
          <n-button :disabled="playerStore.money < item.price" @click="buyItem(item)">
            购买
          </n-button>
        </template>
      </n-list-item>
    </n-list>
  </n-flex>
</template>

<script lang="ts" setup>
import {NButton, NFlex, NH1, NList, NListItem, NPageHeader, NThing, useMessage} from 'naive-ui';
import {useShopStore} from '../stores/shopStore';
import {usePlayerStore} from '../stores/playerStore';
import {type Item} from '#/types/models';
import {useWorkflowStream} from '@yaesandbox-frontend/core-services/composables';
import WorkflowProviderButton from "#/components/WorkflowProviderButton.vue";
import type {WorkflowConfig} from "@yaesandbox-frontend/core-services/types";
import {computed, ref} from "vue";
import {nanoid} from "nanoid";
import {getText, getThink, parseNumber} from "#/utils/workflowParser.ts";
import type {StreamedItem} from "#/types/streaming.ts";

const shopStore = useShopStore();
const playerStore = usePlayerStore();
const message = useMessage();

// 用于绑定输入框内容的 ref
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
  ].filter(Boolean); // 过滤掉 null
  return thoughts.join('\n');
});

async function handleGenerateItem(config: WorkflowConfig)
{
  // 增加输入验证
  if (!generationTopic.value.trim())
  {
    message.warning('请输入物品的描述！');
    return;
  }

  console.log(`使用主题 "${generationTopic.value}" 开始执行工作流...`, config);
  const inputs = {
    // 将输入框的值作为 workflow input
    topic: generationTopic.value,
  };
  await executeItemGeneration(config, inputs);
}

function addGeneratedItemToShop()
{
  if (generatedItem.value)
  {
    const newItem: Item = {
      id: nanoid(),
      name: getText(generatedItem.value.name) || '未命名商品',
      description: getText(generatedItem.value.description) || '无描述',
      price: parseNumber(generatedItem.value.price) || 100,
    };
    shopStore.itemsForSale.push(newItem);
    message.success(`“${newItem.name}”已成功添加到商店！`);
    clearGeneratedItem();
  }
}

function clearGeneratedItem()
{
  generatedItem.value = null;
}

function buyItem(item: Item)
{
  if (playerStore.spendMoney(item.price))
  {
    playerStore.addItem(item);
    message.success(`成功购买 ${item.name}!`);
  }
  else
  {
    message.error('金钱不足！');
  }
}
</script>