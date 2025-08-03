<template>
  <div class="message-input-container">
    <div class="input-area">
      <n-input
          type="textarea"
          v-model:value="userInput"
          :placeholder="inputPlaceholder"
          :autosize="{ minRows: 1, maxRows: 5 }"
          :disabled="loading"
          @keydown="handleKeyDown"
      />
      <n-button
          type="primary"
          @click="sendMessage"
          :loading="loading"
          :disabled="isSendDisabled"
      >
        发送
      </n-button>
    </div>
    <div class="input-settings">
      <n-space align="center" size="small">
        <n-tooltip trigger="hover">
          <template #trigger>
            <n-switch v-model:value="sendWithEnter" />
          </template>
          {{ sendWithEnter ? '当前: Enter 发送消息' : '当前: Shift+Enter 发送消息' }}
        </n-tooltip>
        <label>使用 Enter 键发送</label>
      </n-space>
    </div>
  </div>
</template>

<script lang="ts" setup>
import { ref, computed } from 'vue';
import { NInput, NButton, NSwitch, NSpace, NTooltip } from 'naive-ui';

const props = defineProps<{
  loading: boolean;
}>();

const emit = defineEmits(['send-message']);

const userInput = ref('');
// 新增：控制发送行为的开关，默认为 false (Shift+Enter键发送)
const sendWithEnter = ref(false);

const isSendDisabled = computed(() => props.loading || !userInput.value.trim());

// 新增：动态的 placeholder
const inputPlaceholder = computed(() => {
  if (sendWithEnter.value) {
    return '输入消息... (Enter 发送, Shift + Enter 换行)';
  } else {
    return '输入消息... (Shift + Enter 发送, Enter 换行)';
  }
});

// 重构：将发送逻辑提取为独立函数
function sendMessage() {
  if (isSendDisabled.value) return;
  emit('send-message', userInput.value);
  userInput.value = '';
}

// 重构：键盘事件处理函数
function handleKeyDown(event: KeyboardEvent) {
  // 我们只关心 Enter 键
  if (event.key !== 'Enter') {
    return;
  }

  // 根据开关状态决定是否发送
  const shouldSendWithEnter = sendWithEnter.value && !event.shiftKey;
  const shouldSendWithShiftEnter = !sendWithEnter.value && event.shiftKey;

  if (shouldSendWithEnter || shouldSendWithShiftEnter) {
    // 阻止默认行为（换行）
    event.preventDefault();
    sendMessage();
  }
  // 在其他情况下 (例如：需要用Enter换行时)，我们不执行任何操作，
  // 让浏览器执行默认的换行行为。
}
</script>

<style scoped>
.message-input-container {
  display: flex;
  flex-direction: column; /* 垂直布局，设置在上方 */
  gap: 8px;
  padding: 12px 16px;
  border-top: 1px solid #e8e8e8;
  background-color: #fff;
}

.input-area {
  display: flex;
  align-items: flex-start;
  gap: 8px;
}

.input-settings {
  display: flex;
  justify-content: flex-end; /* 将设置项对齐到右边 */
  font-size: 12px;
  color: #666;
}

.input-settings label {
  cursor: pointer;
}
</style>