<!-- src/features/chat/ChatMessageDisplay.vue -->
<template>
  <div v-if="message" :class="['message-wrapper', `role-${message.role.toLowerCase()}`]">
    <!-- 头像 (左) -->
    <n-avatar v-if="isAssistant" :size="32" round style="margin-right: 8px;">
      {{ avatar }}
    </n-avatar>

    <div class="message-content">
      <n-card :bordered="false" content-style="padding: 10px 14px; white-space: pre-wrap; word-break: break-word;" size="small">
        <!-- 编辑模式 -->
        <n-flex v-if="isEditing" vertical>
          <n-input v-model:value="editingText" autosize type="textarea"/>
          <n-space justify="end">
            <n-button size="tiny" @click="handleCancelEdit">取消</n-button>
            <n-button size="tiny" type="primary" @click="handleSaveEdit">保存</n-button>
            <n-button size="tiny" type="primary" @click="handleSaveAndResubmit">保存并重新生成</n-button>
          </n-space>
        </n-flex>

        <!-- 展示模式 -->
        <template v-else>
          <ContentRenderer :content="displayContent"/>
        </template>
      </n-card>

      <!-- 动作和分支切换 -->
      <div class="message-actions">
        <!-- 分支切换 -->
        <n-space v-if="siblings.length > 1" align="center" size="small">
          <n-button :disabled="isFirstSibling" circle size="tiny" text @click="switchToSibling(-1)">
            <template #icon>
              <n-icon :component="ChevronLeftIcon"/>
            </template>
          </n-button>
          <span>{{ currentSiblingIndex + 1 }} / {{ siblings.length }}</span>
          <n-button :disabled="isLastSibling" circle size="tiny" text @click="switchToSibling(1)">
            <template #icon>
              <n-icon :component="ChevronRightIcon"/>
            </template>
          </n-button>
        </n-space>

        <!-- 操作按钮 -->
        <n-space>
          <n-button size="tiny" text @click="handleEdit">
            <template #icon>
              <n-icon :component="EditIcon"/>
            </template>
          </n-button>
          <n-button v-if="isAssistant" size="tiny" text @click="handleRegenerate">
            <template #icon>
              <n-icon :component="RefreshIcon"/>
            </template>
          </n-button>
          <n-button size="tiny" text @click="handleDelete">
            <template #icon>
              <n-icon :component="DeleteIcon"/>
            </template>
          </n-button>
        </n-space>
      </div>
    </div>

    <!-- 头像 (右) -->
    <n-avatar v-if="isUser" :size="32" round style="margin-left: 8px;">
      {{ avatar }}
    </n-avatar>
  </div>
</template>

<script lang="ts" setup>
import {computed, ref, watch} from 'vue';
import {NAvatar, NButton, NCard, NFlex, NIcon, NInput, NSpace, useDialog} from 'naive-ui';
import {useChatStore} from './chatStore.ts';
import {useCharacterStore} from '#/features/characters/characterStore.ts';
import {ChevronLeftIcon, ChevronRightIcon, DeleteIcon, EditIcon, RefreshIcon} from '@yaesandbox-frontend/shared-ui/icons'
import {ContentRenderer} from "@yaesandbox-frontend/shared-ui/content-renderer";
import {defaultTransformMessageContent} from "#/features/chat/messageTransformer.ts";

const props = defineProps<{
  messageId: string,
  customTransformFunction?: (content: string) => string,
}>();

const emit = defineEmits<{
  (e: 'regenerate', parentMessageId: string): void
  (e: 'edit-and-resubmit', messageId: string, newContent: string): void
}>();

const chatStore = useChatStore();
const characterStore = useCharacterStore();
const dialog = useDialog();

// --- 核心数据 ---
const message = computed(() => chatStore.messages[props.messageId]);

// --- 状态 ---
const isUser = computed(() => message.value?.role === 'User');
const isAssistant = computed(() => message.value?.role === 'Assistant');
const isEditing = ref(false);
const editingText = ref('');

watch(message, (newMessage) =>
{
  if (newMessage)
  {
    // 编辑时总是使用原始内容
    editingText.value = newMessage.data.content;
  }
}, {immediate: true});


const displayContent = computed(() =>
{
  if (!message.value) return '';

  const rawContent = message.value.data.content;

  // 优先使用自定义转换函数
  if (props.customTransformFunction) {
    try {
      return props.customTransformFunction(rawContent);
    } catch (e) {
      console.error("执行自定义内容转换脚本失败，已回退到默认转换器:", e);
      // 如果自定义函数执行失败，则使用默认函数作为后备
      return defaultTransformMessageContent(rawContent);
    }
  }

  // 如果没有提供自定义函数，则使用默认函数
  return defaultTransformMessageContent(rawContent);
});


// --- 计算属性 ---
const avatar = computed(() =>
{
  if (!message.value) return '?';
  const session = chatStore.sessions.find(s => s.id === message.value.sessionId);
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

// --- 分支逻辑 ---
const siblings = computed(() => message.value ? chatStore.findMessageSiblings(props.messageId) : []);
const currentSiblingIndex = computed(() => siblings.value.findIndex(s => s.id === props.messageId));
const isFirstSibling = computed(() => currentSiblingIndex.value <= 0);
const isLastSibling = computed(() => currentSiblingIndex.value >= siblings.value.length - 1);

function switchToSibling(direction: 1 | -1)
{
  const targetIndex = currentSiblingIndex.value + direction;
  if (targetIndex >= 0 && targetIndex < siblings.value.length)
  {
    const targetSibling = siblings.value[targetIndex];
    chatStore.setActiveLeaf(targetSibling.sessionId, targetSibling.id);
  }
}

// --- CURD 操作 ---
function handleEdit()
{
  isEditing.value = true;
}

function handleCancelEdit()
{
  // 退出编辑时，恢复原始文本，防止用户输入被意外保留
  if (message.value)
  {
    editingText.value = message.value.data.content;
  }
  isEditing.value = false;
}

function handleSaveEdit()
{
  if (message.value)
  {
    // 直接调用 store action 来更新内容
    chatStore.updateMessageData(props.messageId, {content: editingText.value});
  }
  isEditing.value = false;
}


function handleSaveAndResubmit()
{
  if (message.value)
  {
    emit('edit-and-resubmit', props.messageId, editingText.value);
  }
  isEditing.value = false;
}

function handleRegenerate()
{
  if (message.value?.parentId)
  {
    emit('regenerate', message.value.parentId);
  }
}

function handleDelete()
{
  if (!message.value) return;
  dialog.warning({
    title: '确认删除',
    content: `你确定要删除这条消息及其所有后续分支吗？此操作不可逆。`,
    positiveText: '确定',
    negativeText: '取消',
    onPositiveClick: () =>
    {
      chatStore.deleteMessageAndChildren(props.messageId);
    },
  });
}
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

.message-think {
  margin-bottom: 8px;
}

.message-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
  margin-top: 4px;
  opacity: 0;
  transition: opacity 0.2s;
  padding: 0 8px;
  min-height: 24px;
}

.message-wrapper:hover .message-actions {
  opacity: 1;
}
</style>