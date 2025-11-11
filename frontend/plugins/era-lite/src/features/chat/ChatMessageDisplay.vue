<!-- src/features/chat/ChatMessageDisplay.vue -->
<template>
  <div v-if="isVisible" :class="['message-wrapper', `role-${message.role.toLowerCase()}`]">
    <!-- 消息头：包含头像和角色名 -->
    <div v-if="character" class="message-header">
      <CharacterAvatar :character="character" :size="32"/>
      <span class="character-name">{{ character.name }}</span>
    </div>

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
          <n-button
              size="tiny"
              text
              title="预览脚本渲染效果"
              @click="handlePreviewRender"
          >
            <template #icon>
              <n-icon :component="CodeIcon"/>
            </template>
          </n-button>

          <n-button
              size="tiny"
              text
              title="编辑消息"
              @click="handleEdit"
          >
            <template #icon>
              <n-icon :component="EditIcon"/>
            </template>
          </n-button>
          <n-button
              v-if="isAssistant"
              size="tiny"
              text
              title="重新生成"
              @click="handleRegenerate"
          >
            <template #icon>
              <n-icon :component="RefreshIcon"/>
            </template>
          </n-button>
          <n-button
              size="tiny"
              text
              title="删除消息"
              @click="handleDelete"
          >
            <template #icon>
              <n-icon :component="DeleteIcon"/>
            </template>
          </n-button>
        </n-space>
      </div>
    </div>
  </div>
</template>

<script lang="tsx" setup>
import {computed, type ComputedRef, ref, watch} from 'vue';
import {NButton, NCard, NCode, NFlex, NIcon, NInput, NSpace, useDialog, useThemeVars} from 'naive-ui';
import {useChatStore} from './chatStore.ts';
import {useCharacterStore} from '#/features/characters/characterStore.ts';
import {ChevronLeftIcon, ChevronRightIcon, CodeIcon, DeleteIcon, EditIcon, RefreshIcon} from '@yaesandbox-frontend/shared-ui/icons'
import {ContentRenderer} from "@yaesandbox-frontend/shared-ui/content-renderer";
import {defaultTransformMessageContent} from "#/features/chat/messageTransformer.ts";
import CharacterAvatar from "#/components/CharacterAvatar.vue";
import type {Character} from "#/types/models.ts";

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
const themeVars = useThemeVars();

// --- 核心数据 ---
const message = computed(() => chatStore.messages[props.messageId]);

const isVisible = computed(() =>
{
  if (!message.value)
  {
    return false;
  }
  // 如果是作为根节点的、且内容为空的系统消息，则不显示它
  if (message.value.role === 'System' && message.value.parentId === null && !message.value.data.content)
  {
    return false;
  }
  return true;
});

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
  if (props.customTransformFunction)
  {
    try
    {
      return props.customTransformFunction(rawContent);
    } catch (e)
    {
      console.error("执行自定义内容转换脚本失败，已回退到默认转换器:", e);
      // 如果自定义函数执行失败，则使用默认函数作为后备
      return defaultTransformMessageContent(rawContent);
    }
  }

  // 如果没有提供自定义函数，则使用默认函数
  return defaultTransformMessageContent(rawContent);
});

function handlePreviewRender()
{
  dialog.info({
    title: '脚本处理后源代码预览',
    content: () => (
        <NCode
            code={displayContent.value}
            language="xml"
            wordWrap={true}
        />
    ),
    positiveText: '关闭',
    maskClosable: true,
    style: {
      width: '60vw',
      maxWidth: '800px',
    },
    // 让弹窗内容可以滚动
    contentStyle: {
      maxHeight: '60vh',
      overflow: 'auto'
    }
  });
}


// 计算角色信息
const character: ComputedRef<Character | null> = computed(() =>
{
  if (!message.value) return null;
  const session = chatStore.sessions.find(s => s.id === message.value!.sessionId);
  if (!session) return null;

  let char;
  if (isUser.value)
  {
    char = characterStore.characters.find(c => c.id === session.playerCharacterId);
  }
  else if (isAssistant.value)
  {
    char = characterStore.characters.find(c => c.id === session.targetCharacterId);
  }

  if (char)
    return char;

  // Fallback
  const fallbackName = isUser.value ? '玩家' : '助手';
  return {
    id: `fallback-${message.value.id}`,
    name: fallbackName,
    description: '',
    avatar: isUser.value ? 'U' : 'A',
  };
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
  flex-direction: column;
  margin-bottom: 24px; /* 增加消息间距 */
  max-width: 80%;
}

/* 消息靠右 */
.role-user {
  align-self: flex-end;
  align-items: flex-end; /* 使内部元素也靠右对齐 */
}

/* 消息靠左 */
.role-assistant {
  align-self: flex-start;
  align-items: flex-start;
}

/* 系统消息居中（样式不变） */
.role-system {
  align-self: center;
  max-width: 100%;
  font-style: italic;
  color: #999;
}

/* 消息头 (头像 + 名字) */
.message-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.character-name {
  font-size: 13px;
  font-weight: 600;
  color: v-bind('themeVars.textColor2');
}

/* 用户消息头，名字在左，头像在右 */
.role-user .message-header {
  flex-direction: row-reverse;
}

/* 消息内容区（卡片 + 操作） */
.message-content {
  display: flex;
  flex-direction: column;
  width: 100%;
}

/* 使用 :deep() 来穿透 scoped 样式，修改 Naive UI 组件的内部样式 */
.message-wrapper :deep(.n-card) {
  border-radius: 12px; /* 更大的圆角 */
  box-shadow: v-bind('themeVars.boxShadow1');
  transition: all 0.2s ease-in-out;
}

.message-wrapper:hover :deep(.n-card) {
  box-shadow: v-bind('themeVars.boxShadow2');
  transform: translateY(-2px);
}

/* 助手消息卡片样式 */
.role-assistant :deep(.n-card) {
  background-color: v-bind('themeVars.cardColor');
  border: 1px solid v-bind('themeVars.borderColor');
}

/* 用户消息卡片样式 */
.role-user :deep(.n-card) {
  /* 使用一个柔和的主题色 */
  background-color: v-bind('themeVars.actionColor');
  border: 1px solid v-bind('themeVars.primaryColorHover');
}

/* 消息操作区 */
.message-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
  margin-top: 6px;
  padding: 0 8px;
  min-height: 24px;
  opacity: 0;
  transition: opacity 0.2s;
}

.message-wrapper:hover .message-actions {
  opacity: 1;
}
</style>