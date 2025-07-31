<template>
  <div class="message-input-area">
    <n-input
        type="textarea"
        v-model:value="userInput"
        placeholder="输入消息... (Shift + Enter 换行)"
        :autosize="{ minRows: 1, maxRows: 5 }"
        :disabled="loading"
        @keydown.enter.prevent="handleSend"
    />
    <n-button
        type="primary"
        @click="handleMouseSend"
        :loading="loading"
        :disabled="isSendDisabled"
    >
      发送
    </n-button>
  </div>
</template>

<script lang="ts" setup>
import { ref, computed } from 'vue';
import { NInput, NButton } from 'naive-ui';

const props = defineProps<{
  loading: boolean;
}>();

const emit = defineEmits(['send-message']);

const userInput = ref('');

const isSendDisabled = computed(() => props.loading || !userInput.value.trim());

function handleSend(event: KeyboardEvent) {
  // 允许 Shift + Enter 换行
  if (event.shiftKey) {
    return;
  }

  if (isSendDisabled.value) return;

  emit('send-message', userInput.value);
  userInput.value = '';
}

function handleMouseSend(event: MouseEvent) {
  if (isSendDisabled.value) return;

  emit('send-message', userInput.value);
  userInput.value = '';
}
</script>

<style scoped>
.message-input-area {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 16px;
  border-top: 1px solid #e8e8e8;
}
</style>