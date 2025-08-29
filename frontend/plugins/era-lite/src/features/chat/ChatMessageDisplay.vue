<!-- src/features/chat/ChatMessageDisplay.vue -->
<template>
  <div :class="['message-wrapper', `role-${message.role.toLowerCase()}`]">
    <n-avatar v-if="isAssistant" :size="32" round style="margin-right: 8px;">
      {{ avatar }}
    </n-avatar>
    <div class="message-content">
      <n-card :bordered="false" content-style="padding: 10px 14px;" size="small">
        <div v-if="isAssistant && message.content.think" class="message-actions">
          <n-button size="tiny" text @click="showThink = !showThink">
            <template #icon>
              <n-icon :component="BrainIcon"/>
            </template>
            思考过程
          </n-button>
          <n-collapse-transition :show="showThink">
            <n-log :log="message.content.think" language="text" style="margin-top: 8px;" trim/>
          </n-collapse-transition>
        </div>
        <!-- TODO: 使用 markdown-it 渲染 content -->
        <div style="white-space: pre-wrap; word-break: break-word; min-width: 50px">{{ message.content.content }}</div>
      </n-card>
      <div class="message-actions">
        <!-- WIP: 添加重试、编辑等按钮 -->
      </div>
    </div>
    <n-avatar v-if="isUser" :size="32" round style="margin-left: 8px;">
      {{ avatar }}
    </n-avatar>
  </div>
</template>

<script lang="ts" setup>
import {computed,ref} from 'vue';
import {NAvatar, NCard} from 'naive-ui';
import type {ChatMessage} from '#/types/chat';
import {useChatStore} from './chatStore.ts';
import {useCharacterStore} from '#/features/characters/characterStore.ts';
import {BrainIcon} from "#/utils/icon.ts";

const props = defineProps<{ message: ChatMessage }>();

const chatStore = useChatStore();
const characterStore = useCharacterStore();

const isUser = computed(() => props.message.role === 'User');
const isAssistant = computed(() => props.message.role === 'Assistant');

const showThink = ref(false);

const avatar = computed(() =>
{
  const session = chatStore.sessions.find(s => s.id === props.message.sessionId);
  if (!session) return '?';

  if (isUser.value)
  {
    return characterStore.characters.find(c => c.id === session.playerCharacterId)?.avatar || 'U';
  }
  if (isAssistant.value)
  {
    return characterStore.characters.find(c => c.id === session.targetCharacterId)?.avatar || 'A';
  }
  return 'S';
});
</script>

<style scoped>
.message-wrapper {
  display: flex;
  margin-bottom: 16px;
  max-width: 80%;
}

.role-user {
  align-self: flex-end;
  flex-direction: row-reverse;
}

.role-assistant {
  align-self: flex-start;
}

.role-system {
  align-self: center;
  max-width: 100%;
  font-style: italic;
  color: #999;
}

.message-content {
  display: flex;
  flex-direction: column;
}

.role-user .message-content {
  align-items: flex-end;
}

.role-user .n-card {
  background-color: #cce5ff;
}

.role-assistant .n-card {
  background-color: #f0f0f0;
}

.message-actions {
  font-size: 12px;
  margin-top: 4px;
  opacity: 0;
  transition: opacity 0.2s;
}

.message-wrapper:hover .message-actions {
  opacity: 1;
}
</style>