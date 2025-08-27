<!-- src/components/ConfirmationDialog.vue -->
<script setup lang="ts">
defineProps<{
  title: string;
  message: string; // 允许传入 HTML
}>();

const emit = defineEmits(['confirm', 'cancel']);
</script>

<template>
  <div class="dialog-backdrop" @click.self="emit('cancel')">
    <div class="dialog-content">
      <h2 class="dialog-title">{{ title }}</h2>
      <!--
        使用 v-html 来渲染带链接的消息。
        警告：只有在您完全信任传入的 message 字符串时才使用 v-html。
        在这里是安全的，因为是我们自己在 App.vue 中定义的静态字符串。
      -->
      <p class="dialog-message" v-html="message"></p>
      <div class="dialog-actions">
        <button @click="emit('cancel')" class="button-secondary">取消</button>
        <button @click="emit('confirm')" class="button-primary">确定</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.dialog-backdrop {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-color: rgba(0, 0, 0, 0.6);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
}

.dialog-content {
  background-color: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 5px 15px rgba(0, 0, 0, 0.3);
  max-width: 500px;
  width: 90%;
  text-align: left;
}

.dialog-title {
  margin-top: 0;
  color: #333;
}

.dialog-message {
  color: #555;
  line-height: 1.6;
  white-space: pre-wrap; /* 保持换行符 */
}

/* 让 v-html 里的链接样式更好看 */
.dialog-message :deep(a) {
  color: #007bff;
  text-decoration: none;
}
.dialog-message :deep(a:hover) {
  text-decoration: underline;
}


.dialog-actions {
  margin-top: 1.5rem;
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
}

/* 复用 App.vue 的按钮样式 */
.button-primary, .button-secondary {
  padding: 0.6em 1.2em;
  border-radius: 6px;
  border: 1px solid transparent;
  cursor: pointer;
  font-weight: 500;
}
.button-primary { background-color: #007bff; color: white; }
.button-secondary { background-color: #6c757d; color: white; }
</style>