<!-- era-lite/src/views/HomeView.vue -->
<template>
  <n-flex :size="24" vertical>
    <n-h1>欢迎来到 Era-Lite</n-h1>

    <!-- 当前选择 -->
    <n-grid :cols="2" :x-gap="24">
      <n-gi>
        <character-display-card :character="sessionStore.selectedPlayerCharacter" title="主角"/>
      </n-gi>
      <n-gi>
        <character-display-card :character="sessionStore.selectedTargetCharacter" title="交互对象"/>
      </n-gi>
    </n-grid>

    <n-card title="当前场景">
      <n-empty v-if="!sessionStore.selectedScene" description="尚未选择场景">
        <template #extra>
          <n-button @click="router.push({ name: 'Era_Lite_Scenes' })">去选择</n-button>
        </template>
      </n-empty>
      <div v-else>
        <n-h3 style="margin: 0">{{ sessionStore.selectedScene.name }}</n-h3>
        <p style="white-space: pre-wrap;">{{ sessionStore.selectedScene.description }}</p>
      </div>
    </n-card>

    <!-- 操作按钮 -->
    <n-card title="开始行动">
      <n-flex>
        <n-button :disabled="!sessionStore.isReadyForInteraction" size="large" type="primary" @click="startInteraction">
          开始交互
        </n-button>
        <n-button ghost size="large" @click="sessionStore.clearSelections()">
          清空选择
        </n-button>
      </n-flex>
    </n-card>
  </n-flex>
</template>

<script lang="ts" setup>
import {NButton, NCard, NEmpty, NFlex, NGi, NGrid, NH1, NH3, useMessage} from 'naive-ui';
import {useSessionStore} from '#/stores/sessionStore.ts';
import CharacterDisplayCard from './CharacterDisplayCard.vue';
import {useRouter} from "vue-router";
import {useChatStore} from '#/features/chat/chatStore.ts';

const router = useRouter();
const sessionStore = useSessionStore();
const chatStore = useChatStore();
const message = useMessage();

function startInteraction()
{
  if (!sessionStore.isReadyForInteraction)
  {
    message.warning('请先选择主角、交互对象和场景。');
    return;
  }

  try
  {
    const newSessionId = chatStore.createChatSession(
        sessionStore.playerCharacterId!,
        sessionStore.targetCharacterId!,
        sessionStore.currentSceneId!
    );

    message.success('新对话已创建！');
    router.push({name: 'Era_Lite_Chat_View', params: {sessionId: newSessionId}});

  } catch (error: any)
  {
    message.error(`无法开始交互: ${error.message}`);
  }
}
</script>