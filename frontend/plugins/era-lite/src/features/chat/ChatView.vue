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
              <template #icon>
                <n-icon :component="EarthIcon"/>
              </template>
              场景: {{ scene.name }}
            </n-tag>
            <n-flex align="center" size="small" style="margin-left: 16px;">
              <label for="filter-think-switch" style="font-size: 14px; color: #666; cursor: pointer;">过滤思考</label>
              <n-switch id="filter-think-switch" v-model:value="filterThinkEnabled"/>
              <n-popover trigger="hover">
                <template #trigger>
                  <n-icon :component="HelpCircleIcon" style="cursor: help; color: #999;"/>
                </template>
                <span>在发送给AI时，自动移除历史消息中的 <code>&lt;think&gt;...&lt;/think&gt;</code> 标签及其内容。本地对话记录不受影响。</span>
              </n-popover>
              <n-button
                  :type="scriptError ? 'error' : undefined"
                  block
                  quaternary
                  title="自定义内容显示脚本"
                  @click="isScriptModalVisible = true"
              >
                <template #icon>
                  <n-icon :component="CodeIcon"/>
                </template>
                自定义内容显示脚本
              </n-button>
            </n-flex>
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
              :custom-transform-function="compiledCustomFunction || undefined"
              :message-id="msg.id"
              @regenerate="handleRegenerate"
              @edit-and-resubmit="handleEditAndResubmit"
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
        <WorkflowSelectorButton
            ref="workflowBtnRef"
            :disabled="!userInput.trim() || isLoading"
            :filter="workflowFilter"
            :storage-key="`chat-workflow-${sessionId}`"
            @click="handleSend"
        />
      </div>

      <ResizableMonacoEditorModal
          v-model:script="customScript"
          v-model:show="isScriptModalVisible"
          :default-script="defaultScriptExample"
          expected-function-name="transformContent"
          storage-key-prefix="chat-view-script"
          title="自定义内容显示脚本"/>
    </n-layout-content>
  </n-layout>
</template>

<script lang="ts" setup>
import {computed, nextTick, ref, watch} from 'vue';
import {useRoute, useRouter} from 'vue-router';
import {NButton, NFlex, NH2, NIcon, NInput, NLayout, NLayoutContent, NLayoutHeader, NPageHeader, NSpace} from 'naive-ui';
import {useChatStore} from './chatStore.ts';
import ChatMessageDisplay from './ChatMessageDisplay.vue';
import type {WorkflowConfig} from "@yaesandbox-frontend/core-services/types";
import {useScopedStorage, useStructuredWorkflowStream, type WorkflowFilter} from "@yaesandbox-frontend/core-services/composables";
import {WorkflowSelectorButton} from '@yaesandbox-frontend/core-services/workflow'
import {useCharacterStore} from "#/features/characters/characterStore.ts";
import {useSceneStore} from "#/features/scenes/sceneStore.ts";
import {EarthIcon} from "#/utils/icon.ts";
import {CodeIcon, HelpCircleIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {defaultTransformMessageContent} from "#/features/chat/messageTransformer.ts";
import ResizableMonacoEditorModal from "#/features/chat/ResizableMonacoEditorModal.vue";
import {useScriptCompiler} from "#/features/chat/useScriptCompiler.ts";

const workflowFilter = ref<WorkflowFilter>({
  expectedInputs: ['history_json', 'playerCharacter_json', 'targetCharacter_json', 'scene_json'],
  requiredTags: ['聊天'],
});


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
const workflowBtnRef = ref<InstanceType<typeof WorkflowSelectorButton> | null>(null);
const filterThinkEnabled = useScopedStorage("chat-view-filter-think-enabled", true);

// --- 自定义脚本逻辑 ---
const customScript = useScopedStorage(`chat-view-custom-script`, '');
const isScriptModalVisible = ref(false);

const defaultScriptExample = computed(() => defaultTransformMessageContent.toString()
    // 将导出的函数名 `defaultTransformMessageContent` 替换为约定的函数名 `transformContent`
    .replace('function defaultTransformMessageContent', 'function transformContent')
);

const {compiledFunction: compiledCustomFunction, error: scriptError} = useScriptCompiler({
  scriptRef: customScript,
  expectedFunctionName: 'transformContent'
});

// --- 流式工作流逻辑 ---
const {
  xmlLikeString: streamingResponse,
  isLoading,
  isFinished,
  execute,
} = useStructuredWorkflowStream({xmlToStringPath: ['content']});

// 只有在 streamingResponse 确实存在的情况下（即我们请求了它），才建立 watch
if (streamingResponse)
{
  watch(streamingResponse, (newValue) =>
  {
    // 内部逻辑是完美的，不需要改变
    if (!newValue) return; // 可以在流开始前或结束后忽略空值

    const lastMessage = activeHistory.value[activeHistory.value.length - 1];

    // 确保我们只更新正在进行的、来自AI的消息
    if (lastMessage && lastMessage.role === 'Assistant' && !isFinished.value)
    {
      // 这里的更新逻辑可以更精细，比如只更新 content 字段
      const newPayload = {...lastMessage.data, content: newValue};
      chatStore.updateMessageData(lastMessage.id, newPayload);
    }
  });
}

// 示例：当流结束时，可能需要做最终的确认或保存
watch(isFinished, (finished) =>
{
  if (finished && streamingResponse?.value)
  {
    // TODO 可以在这里进行一次最终的、完整的消息更新，确保数据一致性
    console.log("Stream finished. Final content:", streamingResponse.value);
  }
})

// --- 私有执行函数，用于复用 ---
async function _executeWorkflow(config: WorkflowConfig, historyLeafId: string)
{
  if (!sessionInfo.value) return;

  // 1. 创建AI消息占位符
  const assistantMessageId = chatStore.createAssistantMessagePlaceholder(sessionId.value, historyLeafId);

  // 2. 准备工作流输入
  const history = chatStore.getHistoryFromLeaf(historyLeafId)
      .filter(msg => msg.role !== 'System') // 通常不把System Prompt发给模型
      .map(msg =>
      {
        let content = msg.data.content;
        if (filterThinkEnabled.value && content)
        {
          content = removeThinkTags(content);
        }
        return {
          role: msg.role,
          name: msg.name,
          content: content,
        };
      });

  const inputs = {
    history_json: JSON.stringify(history),
    playerCharacter_json: JSON.stringify(playerCharacter.value),
    targetCharacter_json: JSON.stringify(targetCharacter.value),
    scene_json: JSON.stringify(scene.value),
  };

  // 3. 执行工作流
  await execute(config, inputs);
}

/**
 * 从原始字符串中移除所有 <think>...</think> 和 <thinking>...</thinking> 标签及其内容。
 * 这个函数使用一个简单的状态机（嵌套级别计数器）来正确处理嵌套标签，这是简单的正则表达式无法做到的。
 *
 * 例如，对于输入:
 * "这是公开内容。<think>这是第一层思考<thinking>这是第二层思考</thinking>思考完毕</think>又是公开内容。"
 * 它会正确地移除整个嵌套块，返回:
 * "这是公开内容。又是公开内容。"
 *
 * 另一个复杂的例子，AI可能生成的文本:
 * "<think>用户让我用<think>这个词来标记思考</think>好的"
 * 它也能正确处理，因为内部的 "<think>" 只是文本，不会被误认为标签。
 *
 * @param rawContent 包含类XML标签的原始字符串。
 * @returns 移除了所有 think/thinking 块之后的新字符串。
 */
function removeThinkTags(rawContent: string): string
{
  if (!rawContent || !rawContent.includes('<'))
  {
    return rawContent;
  }

  let result = '';
  let nestingLevel = 0;
  let i = 0;

  const openTags = ['<think>', '<thinking>'];
  const closeTags = ['</think>', '</thinking>'];

  while (i < rawContent.length)
  {
    let tagFound = false;

    // 检查是否匹配任何一个开放标签
    for (const tag of openTags)
    {
      if (rawContent.substring(i, i + tag.length).toLowerCase() === tag)
      {
        nestingLevel++;
        i += tag.length;
        tagFound = true;
        break;
      }
    }
    if (tagFound)
    {
      continue;
    }

    // 检查是否匹配任何一个闭合标签
    for (const tag of closeTags)
    {
      if (rawContent.substring(i, i + tag.length).toLowerCase() === tag)
      {
        // 只有在嵌套级别大于0时才减少，防止错误的闭合标签导致级别变为负数
        if (nestingLevel > 0)
        {
          nestingLevel--;
        }
        i += tag.length;
        tagFound = true;
        break;
      }
    }
    if (tagFound)
    {
      continue;
    }

    // 如果当前不在任何 think 标签内，则将当前字符追加到结果中
    if (nestingLevel === 0)
    {
      result += rawContent[i];
    }

    i++;
  }

  // 最后清理一下可能因标签移除而产生的多余空白
  return result.trim();
}

// --- 事件处理 ---

async function handleSend(config: WorkflowConfig)
{
  const content = userInput.value.trim();
  if (!content || isLoading.value || !sessionInfo.value) return;

  const userMessage = chatStore.addUserMessage(sessionId.value, content);
  userInput.value = '';

  await _executeWorkflow(config, userMessage.id);
}

async function handleRegenerate(parentMessageId: string)
{
  if (isLoading.value) return;
  const config = workflowBtnRef.value?.selectedWorkflowConfig;
  if (!config) return;

  // 将激活分支切回父节点
  chatStore.setActiveLeaf(sessionId.value, parentMessageId);
  // 从父节点开始重新执行
  await _executeWorkflow(config, parentMessageId);
}

async function handleEditAndResubmit(messageId: string, newContent: string)
{
  if (isLoading.value) return;
  const config = workflowBtnRef.value?.selectedWorkflowConfig;
  if (!config) return;

  // 1. 在 store 中更新消息内容，并删除其后代
  chatStore.editMessageContent(messageId, newContent);
  // 2. editMessageContent 会自动把 activeLeaf 切到 messageId，我们直接从这里开始执行
  await _executeWorkflow(config, messageId);
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