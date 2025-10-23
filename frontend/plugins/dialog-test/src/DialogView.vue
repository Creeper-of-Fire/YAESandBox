<template>
  <div class="dialog-view">
    <div class="workflow-selector-wrapper">
      <WorkflowSelectorButton
          ref="workflowSelectorBtnRef"
          :filter="workflowFilter"
          storage-key="dialog-test-workflow"
          @click="handleWorkflowExecution"
      />
    </div>

    <ChatHistory :history="chatHistory"/>

    <MessageInput
        ref="messageInputRef"
        :loading="isLoading"
        @send-message="handleSendMessage"
    />
  </div>
</template>

<script lang="ts" setup>
import {computed, inject, ref, watch} from 'vue';
import {useMessage, useThemeVars} from 'naive-ui';
import {v4 as uuidv4} from 'uuid';
import type {ChatMessage, Prompt} from '#/types';
import {executeWorkflowStream} from '@yaesandbox-frontend/core-services';
import ChatHistory from '#/components/ChatHistory.vue';
import MessageInput from '#/components/MessageInput.vue';
import {TokenResolverKey} from "@yaesandbox-frontend/core-services/inject-key";
import {WorkflowSelectorButton} from "@yaesandbox-frontend/core-services/workflow";
import {useStructuredWorkflowStream, type WorkflowFilter} from "@yaesandbox-frontend/core-services/composables";

const message = useMessage();
const chatHistory = ref<ChatMessage[]>([]);

// 定义工作流筛选条件
// 对话场景需要的工作流必须能处理 'userInput' 和 'history'
const workflowFilter = ref<WorkflowFilter>({
  expectedInputs: ['userInput', 'history'],
  requiredTags: ['chat'] // 推荐但不强制要求有 'chat' 标签
});
const workflowSelectorBtnRef = ref<InstanceType<typeof WorkflowSelectorButton> | null>(null);
const selectedWorkflow = computed(() => workflowSelectorBtnRef.value?.selectedWorkflowConfig);

const {
  xmlLikeString: streamingResponse, // 我们关心的数据流
  isLoading,                       // Composable 管理的加载状态
  isFinished,                      // Composable 管理的完成状态
  error: streamError,              // Composable 管理的错误状态
  execute,                         // 触发执行的函数
} = useStructuredWorkflowStream({
  xmlToStringPath: [] // 指定从根路径转换内容为字符串
});

if (streamingResponse)
{
  // 响应式地将流数据更新到UI
  watch(streamingResponse, (newContent) =>
  {
    if (!newContent || isFinished.value) return; // 忽略空值或已结束的流

    const lastMessage = chatHistory.value[chatHistory.value.length - 1];
    // 确保我们只更新正在进行的、来自AI的消息
    if (lastMessage && lastMessage.role === 'Assistant' && isLoading.value)
    {
      lastMessage.content = newContent;
    }
  });
}

// 响应式地处理流错误
watch(streamError, (err) =>
{
  if (!err) return;

  const errorText = `[Stream Error] ${err.message}`;
  message.error(errorText);

  const lastMessage = chatHistory.value[chatHistory.value.length - 1];
  if (lastMessage && lastMessage.role === 'Assistant')
  {
    lastMessage.content = errorText;
  }
});

const messageInputRef = ref<InstanceType<typeof MessageInput> | null>(null);

function handleWorkflowExecution()
{
  const currentInput = messageInputRef.value?.getCurrentInput();

  if (currentInput && currentInput.trim())
  {
    // 如果输入框有内容，直接触发发送
    messageInputRef.value?.triggerSend();
  }
  else
  {
    // 如果输入框为空，给出提示
    message.info('AI 已就绪，请输入您想说的内容。');
  }
}

async function handleSendMessage(userInput: string)
{
  const config = selectedWorkflow.value;
  if (!config)
  {
    message.warning('请先选择一个工作流！');
    return;
  }
  if (isLoading.value) return;

  // a. 更新UI，添加用户消息和AI占位符
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

  // b. 准备工作流输入
  const historyPrompt: Prompt[] = chatHistory.value
      .slice(0, -2) // 排除用户刚输入的消息和AI占位符，获取之前的历史
      .map(msg => ({ role: msg.role, content: msg.content }));

  const workflowInputs = {
    history: JSON.stringify(historyPrompt),
    userInput: userInput,
  };

  // c. 调用 Composable 的 execute 函数，触发工作流
  await execute(config, workflowInputs);
}

const themeVars = useThemeVars();
</script>

<style scoped>
.dialog-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  width: 100%;
}

.workflow-selector-wrapper {
  padding: 16px;
  border-left: 1px solid v-bind('themeVars.borderColor');
}
</style>