<template>
  <div class="dialog-view">
    <DialogWorkflowSelector @workflow-selected="handleWorkflowSelected"/>

    <ChatHistory :history="chatHistory"/>

    <MessageInput
        :loading="isLoading"
        @send-message="handleSendMessage"
    />
  </div>
</template>

<script lang="ts" setup>
import {inject, ref} from 'vue';
import {useMessage} from 'naive-ui';
import {v4 as uuidv4} from 'uuid';

import type {WorkflowConfig} from '@yaesandbox-frontend/core-services/types';
import type {ChatMessage, Prompt} from '#/types';
import {executeWorkflowStream} from '@yaesandbox-frontend/core-services';

import DialogWorkflowSelector from '#/components/DialogWorkflowSelector.vue';
import ChatHistory from '#/components/ChatHistory.vue';
import MessageInput from '#/components/MessageInput.vue';
import {TokenResolverKey} from "@yaesandbox-frontend/core-services/injectKeys";

const message = useMessage();
const isLoading = ref(false);
const chatHistory = ref<ChatMessage[]>([]);
const selectedWorkflow = ref<{ id: string; config: WorkflowConfig } | null>(null);

function handleWorkflowSelected(payload: { id: string; config: WorkflowConfig })
{
  selectedWorkflow.value = payload;
  chatHistory.value = [];
  message.success(`已选择工作流: ${payload.config.name}`);
}

const tokenResolver = inject(TokenResolverKey);

async function handleSendMessage(userInput: string)
{
  if (!tokenResolver)
  {
    message.error('未注入 TokenResolver');
    return;
  }
  if (!selectedWorkflow.value)
  {
    message.warning('请先选择一个工作流！');
    return;
  }
  if (isLoading.value) return;

  isLoading.value = true;

  chatHistory.value.push({
    id: uuidv4(),
    role: 'User',
    content: userInput,
  });

  const assistantMessageId = uuidv4();
  chatHistory.value.push({
    id: assistantMessageId,
    role: 'Assistant',
    content: '...',
  });

  const historyPrompt: Prompt[] = chatHistory.value
      .slice(0, -1)
      .map(msg => ({role: msg.role, content: msg.content}));

  const workflowInputs = {
    history: JSON.stringify(historyPrompt),
    userInput: userInput,
  };

  const requestBody = {
    workflowConfig: selectedWorkflow.value.config,
    workflowInputs: workflowInputs,
  };

  await executeWorkflowStream(requestBody, {
        onMessage: (updatedContent) =>
        {
          const assistantMessage = chatHistory.value.find(m => m.id === assistantMessageId);
          if (assistantMessage)
          {
            assistantMessage.content = updatedContent;
          }
        },
        onClose: () =>
        {
          isLoading.value = false;
        },
        onError: (error) =>
        {
          const errorText = `[Stream Error] ${error.message}`;
          message.error(errorText);
          const assistantMessage = chatHistory.value.find(m => m.id === assistantMessageId);
          if (assistantMessage)
          {
            assistantMessage.content = errorText;
          }
          isLoading.value = false;
        },
      },
      tokenResolver
  );
}
</script>

<style scoped>
.dialog-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
}
</style>