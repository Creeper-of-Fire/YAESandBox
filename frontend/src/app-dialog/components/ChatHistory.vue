<template>
  <!-- 使用 :scrollbar-inst-ref 来绑定实例 -->
  <n-scrollbar class="chat-history-area" :scrollbar-inst-ref="scrollbarInstRef">
    <div class="message-list">
      <div v-if="history.length === 0" class="empty-chat">
        <n-empty description="还没有消息，开始对话吧！" />
      </div>
      <div
          v-for="message in displayHistory"
          :key="message.id"
          class="message-wrapper"
          :class="`role-${message.role}`"
      >
        <div class="message-bubble">
          <!-- 这里我们将解析和渲染<think>标签 -->
          <pre v-if="message.thinking" class="thinking-block">{{ message.thinking }}</pre>
          <p>{{ message.displayText }}</p>
        </div>
      </div>
    </div>
  </n-scrollbar>
</template>

<script lang="ts" setup>
import { ref, watch, nextTick, computed } from 'vue';
// 1. 导入 ScrollbarInst 类型
import type { ScrollbarInst } from 'naive-ui';
import { NScrollbar, NEmpty } from 'naive-ui';
import type { ChatMessage } from '../types';

// 定义一个更丰富的消息类型，用于在前端分离思维过程和显示文本
type DisplayMessage = {
  id: string;
  role: 'User' | 'Assistant';
  originalContent: string;
  thinking: string | null;
  displayText: string;
}

const props = defineProps<{
  history: ChatMessage[];
}>();

// 2. 创建一个用于接收 ScrollbarInst 实例的 ref
const scrollbarInstRef = ref<ScrollbarInst | null>(null);

// 使用 computed 属性来处理后端返回的文本，将其解析为 DisplayMessage
const displayHistory = computed<DisplayMessage[]>(() => {
  const thinkRegex = /<think>([\s\S]*?)<\/think>\n?([\s\S]*)/;
  return props.history.map(msg => {
    if (msg.role === 'Assistant') {
      const match = msg.content.match(thinkRegex);
      if (match) {
        return {
          id: msg.id,
          role: msg.role,
          originalContent: msg.content,
          thinking: match[1].trim(),
          displayText: match[2].trim(),
        };
      }
    }
    // 对于用户消息或不含<think>的助手消息
    return {
      id: msg.id,
      role: msg.role,
      originalContent: msg.content,
      thinking: null,
      displayText: msg.content,
    };
  });
});


// 监视历史记录的变化，自动滚动到底部
watch(() => [props.history, displayHistory.value.length], async () => {
      await nextTick();
      const scrollbarInstance = scrollbarInstRef.value;
      if (scrollbarInstance) {
        // **关键修改在这里**
        // 我们不再需要访问 DOM 元素或计算 scrollHeight。
        // 只需让它滚动到一个非常大的数字，它就会自动停在最底部。
        scrollbarInstance.scrollTo({ top: 999999, behavior: 'smooth' });
      }
    },
    { deep: true, flush: 'post' }
);

</script>

<style scoped>
.chat-history-area {
  flex-grow: 1;
  padding: 16px;
  background-color: #f7f7f7;
}
.message-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.message-wrapper {
  display: flex;
  max-width: 80%;
}
.message-bubble {
  padding: 10px 14px;
  border-radius: 12px;
  color: #333;
}
.message-bubble p {
  margin: 0;
  white-space: pre-wrap;
  word-wrap: break-word;
}

.role-user {
  align-self: flex-end;
}
.role-user .message-bubble {
  background-color: #A0E9A9;
  border-top-right-radius: 2px;
}

.role-assistant {
  align-self: flex-start;
}
.role-assistant .message-bubble {
  background-color: #ffffff;
  border: 1px solid #e8e8e8;
  border-top-left-radius: 2px;
}

.empty-chat {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 50vh;
}
</style>