<template>
  <div class="dialog-view">
    <DialogWorkflowSelector @workflow-selected="handleWorkflowSelected" />

    <ChatHistory :history="chatHistory" />

    <MessageInput
        :loading="isLoading"
        @send-message="handleSendMessage"
    />
  </div>
</template>

<script lang="ts" setup>
import { ref } from 'vue';
import { useMessage } from 'naive-ui';
import { v4 as uuidv4 } from 'uuid';

import type { WorkflowConfig } from '@/app-workbench/types/generated/workflow-config-api-client';
// 移除了原来的 WorkflowExecutionService 导入
import type { ChatMessage, Prompt } from '@/app-dialog/types';
import { executeWorkflowStream } from '@/app-dialog/services/streamingService'; // 导入新的流式服务

import DialogWorkflowSelector from '@/app-dialog/components/DialogWorkflowSelector.vue';
import ChatHistory from '@/app-dialog/components/ChatHistory.vue';
import MessageInput from '@/app-dialog/components/MessageInput.vue';

const message = useMessage();
const isLoading = ref(false);
const chatHistory = ref<ChatMessage[]>([]);
const selectedWorkflow = ref<{ id: string; config: WorkflowConfig } | null>(null);

function handleWorkflowSelected(payload: { id: string; config: WorkflowConfig }) {
  selectedWorkflow.value = payload;
  chatHistory.value = [];
  message.success(`已选择工作流: ${payload.config.name}`);
}

async function handleSendMessage(userInput: string) {
  if (!selectedWorkflow.value) {
    message.warning('请先选择一个工作流！');
    return;
  }
  if (isLoading.value) return; // 防止重复发送

  isLoading.value = true;

  // 1. 将用户输入添加到聊天记录
  chatHistory.value.push({
    id: uuidv4(),
    role: 'User',
    content: userInput,
  });

  // 2. 添加一个空的助手消息占位符
  const assistantMessageId = uuidv4();
  chatHistory.value.push({
    id: assistantMessageId,
    role: 'Assistant',
    content: '...', // 初始显示 "..." 或一个加载中的动画
  });

  // 3. 准备工作流参数
  const historyPrompt: Prompt[] = chatHistory.value
      .slice(0, -1) // 发送给后端的历史不应包含刚刚添加的占位符
      .map(msg => ({ role: msg.role, content: msg.content }));

  const triggerParams = {
    history: JSON.stringify(historyPrompt),
    userInput: userInput,
  };

  const requestBody = {
    workflowConfig: selectedWorkflow.value.config,
    triggerParams: triggerParams,
  };

  // 4. 调用新的流式服务
  executeWorkflowStream(requestBody, {
    onMessage: (updatedContent) => {
      // 找到占位符消息并更新其内容
      const assistantMessage = chatHistory.value.find(m => m.id === assistantMessageId);
      if (assistantMessage) {
        assistantMessage.content = updatedContent;
      }
    },
    onClose: () => {
      isLoading.value = false;
    },
    onError: (error) => {
      const errorText = `[Stream Error] ${error.message}`;
      message.error(errorText);
      // 更新占位符消息为错误信息
      const assistantMessage = chatHistory.value.find(m => m.id === assistantMessageId);
      if (assistantMessage) {
        assistantMessage.content = errorText;
      }
      isLoading.value = false;
    },
  });
}
</script>

<style scoped>
.dialog-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
  background: #fff;
}
</style>