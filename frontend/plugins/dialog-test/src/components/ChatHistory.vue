<template>
  <n-scrollbar :scrollbar-inst-ref="scrollbarInstRef" class="chat-history-area">
    <div class="message-list">
      <div v-if="history.length === 0" class="empty-chat">
        <n-empty description="还没有消息，开始对话吧！"/>
      </div>
      <!-- 1. 修改v-for，直接遍历 props.history -->
      <div
          v-for="message in history"
          :key="message.id"
          :class="`role-${message.role.toLowerCase()}`"
          class="message-wrapper"
      >
        <!-- 2. 添加头像 -->
        <n-avatar
            :style="{
              backgroundColor: message.role === 'User' ? '#A0E9A9' : '#ffffff',
              color: message.role === 'User' ? '#333' : '#007AFF'
            }"
            round
        >
          {{ message.role === 'User' ? 'U' : 'A' }}
        </n-avatar>

        <!-- 3. 气泡容器 -->
        <div class="message-bubble">
          <!-- 直接渲染消息内容，不再有复杂的解析 -->
          <p>{{ message.content }}</p>
        </div>
      </div>
    </div>
  </n-scrollbar>
</template>

<script lang="ts" setup>
import {nextTick, ref, watch} from 'vue';
import {NAvatar, NEmpty, NScrollbar, type ScrollbarInst, useThemeVars} from 'naive-ui';
import type {ChatMessage} from '../types';

const props = defineProps<{
  history: ChatMessage[];
}>();

const scrollbarInstRef = ref<ScrollbarInst | null>(null);

// **逻辑简化**
// 由于后端不再返回<think>标签，我们不再需要 `displayHistory` 这个 computed 属性来解析消息。
// 代码现在直接使用 `props.history` 进行渲染，变得更加简洁和高效。

// 监视历史记录的变化，自动滚动到底部
watch(() => props.history.length, async () =>
    {
      await nextTick();
      const scrollbarInstance = scrollbarInstRef.value;
      if (scrollbarInstance)
      {
        scrollbarInstance.scrollTo({top: 999999, behavior: 'smooth'});
      }
    },
    {flush: 'post'}
);
const themeVars = useThemeVars();
</script>

<style scoped>
.chat-history-area {
  flex-grow: 1;
  padding: 16px;
  background-color: v-bind('themeVars.cardColor');
}

.message-list {
  display: flex;
  flex-direction: column;
  gap: 20px; /* 增加消息间的垂直间距 */
}

/* --- 消息行容器 --- */
.message-wrapper {
  display: flex;
  align-items: flex-start; /* 头像和气泡顶部对齐 */
  gap: 10px;
  max-width: 80%;
}

.message-bubble {
  padding: 10px 14px;
  border-radius: 18px; /* 更圆润的边角 */
  box-shadow: v-bind('themeVars.boxShadow1');
}

.message-bubble p {
  margin: 0;
  white-space: pre-wrap;
  word-wrap: break-word;
  line-height: 1.6;
}

/* --- 用户消息样式 (右侧) --- */
.role-user {
  align-self: flex-end;
  /* 头像在右，气泡在左 */
  flex-direction: row-reverse;
}

.role-user .message-bubble {
  background-color: v-bind('themeVars.primaryColor');
  color: v-bind('themeVars.cardColor');
  /* 小技巧：调整一个角的弧度，模拟微信气泡的尾巴 */
  border-top-right-radius: 5px;
}

/* --- 助手消息样式 (左侧) --- */
.role-assistant {
  align-self: flex-start;
}

.role-assistant .message-bubble {
  color: v-bind('themeVars.textColor1');
  border: 1px solid v-bind('themeVars.borderColor');
  /* 小技巧：调整一个角的弧度，模拟微信气泡的尾巴 */
  border-top-left-radius: 5px;
}

.empty-chat {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 50vh;
}
</style>