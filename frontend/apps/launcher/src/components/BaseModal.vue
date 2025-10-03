<!-- src/components/BaseModal.vue -->
<script lang="ts" setup>
// 定义组件的 props 和 emits
const props = defineProps<{
  // 控制模态框的显示，使用 v-model
  modelValue: boolean;
  title: string;
}>();

const emit = defineEmits(['update:modelValue']);

// 关闭模态框的函数
function closeModal()
{
  emit('update:modelValue', false);
}
</script>

<template>
  <!-- 使用 Vue 的 Transition 组件来添加淡入淡出效果 -->
  <Transition name="modal-fade">
    <div v-if="modelValue" class="modal-overlay" @click="closeModal">
      <div class="modal-content" @click.stop>
        <h3>{{ title }}</h3>
        <div class="modal-body">
          <!-- 这里是插槽，允许父组件插入任何内容 -->
          <slot></slot>
        </div>
        <button class="button-secondary" @click="closeModal">关闭</button>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-color: var(--bg-color-overlay);
  color: var(--text-color-primary);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
}

.modal-content {
  background: var(--bg-color-panel);
  padding: 1.5rem 2rem;
  border-radius: 8px;
  max-width: 600px;
  width: 90%;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  box-shadow: var(--shadow-modal);
  /* 添加 transition 效果 */
  transition: all 0.3s ease;
}

.modal-content h3 {
  margin-top: 0;
  border-bottom: 1px solid var(--border-color-divider);
  padding-bottom: 0.5rem;
  user-select: none;
}

.modal-body {
  flex-grow: 1;
  overflow-y: auto;
  line-height: 1.6;
}

.modal-content button {
  margin-top: 1rem;
  align-self: flex-end;
}

/* --- Transition 动画样式 --- */
.modal-fade-enter-from,
.modal-fade-leave-to {
  opacity: 0;
}

.modal-fade-enter-from .modal-content,
.modal-fade-leave-to .modal-content {
  transform: scale(0.95);
}

.modal-fade-enter-active,
.modal-fade-leave-active {
  transition: opacity 0.3s ease;
}

/* --- 按钮样式 --- */
.button-secondary {
  background-color: var(--color-secondary);
  color: var(--text-color-inverted);
  padding: 0.5em 1em;
  border-radius: 4px;
  border: 1px solid var(--color-secondary);
  cursor: pointer;
  user-select: none;
  -webkit-user-select: none;
}

.button-secondary:hover {
  background-color: var(--color-secondary-hover);
}
</style>