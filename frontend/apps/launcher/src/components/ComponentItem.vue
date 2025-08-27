<script setup lang="ts">
import { computed } from 'vue';
import type { Component } from '../stores/updaterStore';
import { useUpdaterStore } from '../stores/updaterStore';

const props = defineProps<{ component: Component }>();
const store = useUpdaterStore();

const buttonText = computed(() => {
  switch (props.component.status) {
    case 'not_installed': return '安装';
    case 'update_available': return '更新';
    case 'uptodate': return '重新安装';
    case 'downloading': return '下载中...';
    case 'pending_install': return '等待安装';
    case 'installing': return '安装中...';
    case 'error': return '重试';
    default: return '操作';
  }
});

const isButtonDisabled = computed(() => {
  // 1. 如果正在安装（文件操作），则禁用所有按钮
  if (store.isInstalling) {
    return true;
  }
  // 2. 如果组件自身正在下载或等待安装，则禁用它自己的按钮
  if (['downloading', 'pending_install'].includes(props.component.status)) {
    return true;
  }
  // 3. 否则，按钮可用
  return false;
});

function handleAction() {
  if (isButtonDisabled.value)
    return;
  if (props.component.id === 'launcher') {
    store.updateLauncher();
  } else {
    // 对于其他所有组件，点击总是触发下载流程
    store.downloadComponent(props.component.id);
  }
}
</script>

<template>
  <li class="component-list-item">
    <div class="info">
      <span class="name">{{ component.name }}</span>
      <span class="version-info">
        <span v-if="component.localVersion" class="local-version">v{{ component.localVersion }}</span>
        <span v-if="component.status === 'update_available'" class="arrow">→</span>
        <span v-if="component.status === 'update_available'" class="remote-version">v{{ component.remoteVersion }}</span>
      </span>
      <div v-if="component.status === 'downloading'" class="progress-bar-container">
        <progress :value="component.progress.percentage" max="100"></progress>
        <span class="progress-text">{{ component.progress.text }}</span>
      </div>
      <div v-if="component.error" class="error-text">
        错误: {{ component.error }}
      </div>
    </div>
    <div class="status-action">
      <span :class="['status-tag', `status-${component.status}`]">{{ component.statusText }}</span>
      <button @click="handleAction" :disabled="isButtonDisabled" class="button-primary">
        {{ buttonText }}
      </button>
    </div>
  </li>
</template>

<style scoped>
/* 保持大部分原有样式，为新元素添加一些样式 */
.component-list-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 0;
  border-bottom: 1px solid #f0f0f0;
}
.component-list-item:last-child {
  border-bottom: none;
}
.info {
  display: flex;
  flex-direction: column;
  flex-grow: 1;
  padding-right: 1rem;
}
.name {
  font-weight: 600;
  font-size: 1rem;
  color: #2c3e50;
}
.version-info {
  font-size: 0.85rem;
  color: #888;
  margin-top: 0.25rem;
}
.local-version { text-decoration: line-through; }
.arrow { margin: 0 0.5em; font-weight: bold; }
.remote-version { color: #28a745; font-weight: bold; }
.status-action { display: flex; align-items: center; gap: 1rem; }
.status-tag {
  font-size: 0.8em;
  padding: 0.2em 0.6em;
  border-radius: 12px;
  font-weight: bold;
  color: #fff;
  width: 50px; /* 固定宽度以便对齐 */
  text-align: center;
}
.status-uptodate { background-color: #28a745; }
.status-update_available { background-color: #ffc107; color: #333; }
.status-not_installed { background-color: #6c757d; }
.status-downloading, .status-installing, .status-pending_install { background-color: #007bff; }
.status-error { background-color: #dc3545; }

.progress-bar-container {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 0.5rem;
}
progress {
  width: 150px;
}
.progress-text {
  font-size: 0.8em;
  color: #555;
  min-width: 120px;
}
.error-text {
  color: #dc3545;
  font-size: 0.85em;
  margin-top: 0.5rem;
}
</style>