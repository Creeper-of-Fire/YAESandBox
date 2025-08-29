<!-- src/features/chat/ChatListView.vue -->
<template>
  <n-flex :size="24" vertical>
    <n-page-header>
      <template #title>
        <n-h1 style="margin:0">对话记录</n-h1>
      </template>
    </n-page-header>

    <n-empty v-if="chatStore.enrichedSessions.length === 0" description="没有对话记录">
      <template #extra>
        <n-button type="primary" @click="router.push({ name: 'Era_Lite_Home' })">
          去主菜单开始新的对话
        </n-button>
      </template>
    </n-empty>

    <n-list v-else bordered clickable hoverable>
      <n-list-item v-for="session in chatStore.enrichedSessions" :key="session.id" @click="navigateToChat(session.id)">
        <n-thing :title="session.title">
          <template #description>
            <n-space size="small">
              <n-tag size="small">主角: {{ session.playerCharacter?.name || '未知' }}</n-tag>
              <n-tag size="small">对象: {{ session.targetCharacter?.name || '未知' }}</n-tag>
              <n-tag size="small">场景: {{ session.scene?.name || '未知' }}</n-tag>
            </n-space>
          </template>
          <template #header-extra>
            <n-time :time="session.createdAt" type="relative" />
          </template>
        </n-thing>
        <template #suffix>
          <n-button circle ghost type="error" @click.stop="handleDelete(session)">
            <template #icon><n-icon :component="DeleteIcon" /></template>
          </n-button>
        </template>
      </n-list-item>
    </n-list>
  </n-flex>
</template>

<script lang="ts" setup>
import { NButton, NEmpty, NFlex, NH1, NIcon, NList, NListItem, NPageHeader, NSpace, NTag, NThing, NTime, useDialog } from 'naive-ui';
import { useRouter } from 'vue-router';
import { useChatStore } from './chatStore.ts';

import type { EnrichedChatSession } from '#/types/chat';
import {DeleteIcon} from "@yaesandbox-frontend/shared-ui/icons";

const router = useRouter();
const chatStore = useChatStore();
const dialog = useDialog();

function navigateToChat(sessionId: string) {
  router.push({ name: 'Era_Lite_Chat_View', params: { sessionId } });
}

function handleDelete(session: EnrichedChatSession) {
  dialog.warning({
    title: '确认删除',
    content: `你确定要删除与 “${session.targetCharacter?.name}” 的对话吗？此操作不可逆。`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () => {
      chatStore.deleteSession(session.id);
    },
  });
}
</script>