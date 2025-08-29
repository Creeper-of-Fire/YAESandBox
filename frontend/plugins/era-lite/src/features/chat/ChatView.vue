<!-- src/features/chat/ChatView.vue -->
<template>
  <n-layout>
    <n-layout-header bordered style="padding: 12px 24px;">
      <n-page-header @back="router.back()">
        <template #title>
          <n-h2 style="margin: 0;">{{ sessionInfo?.title || '加载中...' }}</n-h2>
        </template>
        <template #extra>
          <n-space align="center">
            <n-tag v-if="playerCharacter" :bordered="false" round>
              玩家:
              <n-avatar :size="22" style="margin-left: 8px;">{{ playerCharacter.avatar }}</n-avatar>
              {{ playerCharacter.name }}
            </n-tag>
            <n-tag v-if="targetCharacter" :bordered="false" round>
              目标:
              <n-avatar :size="22" style="margin-left: 8px;">{{ targetCharacter.avatar }}</n-avatar>
              {{ targetCharacter.name }}
            </n-tag>
            <n-tag v-if="scene" :bordered="false" round>
              <template #icon><n-icon :component="EarthIcon"/></template>
              场景: {{ scene.name }}
            </n-tag>
          </n-space>
        </template>
      </n-page-header>
    </n-layout-header>

    <n-layout-content style="height: 100%; display: flex; flex-direction: column;">
      <div ref="scrollContainerRef" class="messages-container">
        <n-flex vertical>
          <ChatMessageDisplay
              v-for="msg in activeHistory"
              :key="msg.id"
              :message="msg"
          />
          <!-- 当AI正在响应时，显示一个加载中的占位符 -->
          <n-spin v-if="isLoading" size="small" style="align-self: flex-start; margin-left: 40px;"/>
        </n-flex>
      </div>

      <div class="input-area">
        <n-input
            v-model:value="userInput"
            :disabled="isLoading"
            autosize
            placeholder="输入你的行动或对话..."
            style="flex-grow: 1;"
            type="textarea"
        />
        <WorkflowProviderButton
            ref="workflowBtnRef"
            :disabled="!userInput.trim() || isLoading"
            :expected-inputs="['history_json', 'playerCharacter_json', 'targetCharacter_json', 'scene_json']"
            :storage-key="`chat-workflow-${sessionId}`"
            @click="handleSend"
        />
      </div>
    </n-layout-content>
  </n-layout>
</template>

<script lang="ts" setup>
import {computed, nextTick, ref, watch} from 'vue';
import {useRoute, useRouter} from 'vue-router';
import {NFlex, NH2, NInput, NLayout, NLayoutContent, NLayoutHeader, NPageHeader} from 'naive-ui';
import {useChatStore} from './chatStore.ts';
import ChatMessageDisplay from './ChatMessageDisplay.vue';
import type {WorkflowConfig} from "@yaesandbox-frontend/core-services/types";
import {useStructuredWorkflowStream} from "#/composables/useStructuredWorkflowStream.ts";
import WorkflowProviderButton from "#/components/WorkflowProviderButton.vue";
import {useCharacterStore} from "#/features/characters/characterStore.ts";
import {useSceneStore} from "#/features/scenes/sceneStore.ts";
import type {MessagePayload} from "#/types/chat.ts";
import {EarthIcon} from "#/utils/icon.ts";

const route = useRoute();
const router = useRouter();
const chatStore = useChatStore();
const characterStore = useCharacterStore();
const sceneStore = useSceneStore();

const sessionId = computed(() => route.params.sessionId as string);
const sessionInfo = computed(() => chatStore.sessions.find(s => s.id === sessionId.value));
const activeHistory = computed(() =>
{
  if (sessionInfo.value)
  {
    return chatStore.getHistoryFromLeaf(sessionInfo.value.activeLeafMessageId);
  }
  return [];
});

const playerCharacter = computed(() =>
    characterStore.characters.find(c => c.id === sessionInfo.value?.playerCharacterId)
);
const targetCharacter = computed(() =>
    characterStore.characters.find(c => c.id === sessionInfo.value?.targetCharacterId)
);
const scene = computed(() =>
    sceneStore.scenes.find(s => s.id === sessionInfo.value?.sceneId)
);

const userInput = ref('');
const scrollContainerRef = ref<HTMLElement | null>(null);
const workflowBtnRef = ref<InstanceType<typeof WorkflowProviderButton> | null>(null);

// --- 流式工作流逻辑 ---
const streamingResponse = ref<Partial<MessagePayload> | null>(null);
const responseSchema = [
  {key: 'content', label: '回复', component: NInput},
  {key: 'think', label: '思考', component: NInput}
];

const {
  isLoading,
  execute,
} = useStructuredWorkflowStream(streamingResponse, responseSchema);

// 监听流式响应的变化，并更新到 store
watch(streamingResponse, (newValue) =>
{
  const lastMessage = activeHistory.value[activeHistory.value.length - 1];
  // 确保我们只更新AI的消息
  if (newValue && lastMessage && lastMessage.role === 'Assistant')
  {
    chatStore.updateMessagePayload(lastMessage.id, newValue);
  }
});

async function handleSend(config: WorkflowConfig)
{
  const content = userInput.value.trim();
  if (!content || isLoading.value || !sessionInfo.value) return;

  // 1. 将用户输入添加到 store
  const userMessage = chatStore.addUserMessage(sessionId.value, content);
  userInput.value = ''; // 清空输入框

  // 2. 创建AI消息占位符
  const assistantMessageId = chatStore.createAssistantMessagePlaceholder(sessionId.value, userMessage.id);

  // 3. 准备工作流输入
  const playerCharacter = characterStore.characters.find(c => c.id === sessionInfo.value!.playerCharacterId);
  const targetCharacter = characterStore.characters.find(c => c.id === sessionInfo.value!.targetCharacterId);
  const scene = sceneStore.scenes.find(s => s.id === sessionInfo.value!.sceneId);
  const history = chatStore.getHistoryFromLeaf(userMessage.id)
      .map(msg => ({
        role: msg.role,
        name: msg.name,
        content: msg.content.content
      }));

  const inputs = {
    history_json: JSON.stringify(history),
    playerCharacter_json: JSON.stringify(playerCharacter),
    targetCharacter_json: JSON.stringify(targetCharacter),
    scene_json: JSON.stringify(scene),
  };

  // 4. 执行工作流
  await execute(config, inputs);
}


// 自动滚动到底部
watch(activeHistory, async () =>
{
  await nextTick();
  const container = scrollContainerRef.value;
  if (container)
  {
    container.scrollTop = container.scrollHeight;
  }
}, {deep: true});
</script>

<style scoped>
.messages-container {
  flex-grow: 1;
  overflow-y: auto;
  padding: 24px;
  display: flex;
  flex-direction: column;
}

.input-area {
  padding: 16px;
  display: flex;
  gap: 12px;
  align-items: flex-end;
  border-top: 1px solid #e0e0e6;
}
</style>