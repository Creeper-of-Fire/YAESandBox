<script setup lang="ts">
import { onMounted } from 'vue';
import { invoke } from '@tauri-apps/api/core';
import ComponentItem from './components/ComponentItem.vue';
import {useUpdaterStore} from "./stores/updaterStore.ts";

const store = useUpdaterStore();

const frontendPath = 'wwwroot';
const backendExePath = 'backend/YAESandBox.AppWeb.exe';

onMounted(() => {
  store.listenForProgress();
  store.initialize();
});

const launchApp = async () => {
  // 检查是否有核心组件的更新未完成
  const hasCoreUpdates = store.coreComponents.some(c => c.status === 'update_available');
  if (hasCoreUpdates) {
    const confirmed = confirm(
        `检测到核心组件有可用更新。\n\n` +
        `在未更新的情况下启动可能会导致应用功能异常或不稳定。\n\n` +
        `您确定要继续启动吗？`
    );
    if (!confirmed) return;
  }

  store.globalStatusMessage = '正在启动本地服务...';
  try {
    await invoke('start_local_backend', {
      frontendRelativePath: frontendPath,
      backendExeRelativePath: backendExePath,
    });
    store.globalStatusMessage = '启动命令已发送。';
  } catch (error) {
    console.error('启动服务失败:', error);
    store.globalStatusMessage = `启动失败: ${String(error)}`;
  }
};
</script>

<template>
  <div class="launcher-container">
    <header>
      <h1>YAESandBox 启动器</h1>
      <p class="status-message" :class="{ 'is-busy': store.isInstalling || store.isDownloading, 'is-error': !!store.globalError }">
        {{ store.globalStatusMessage }}
      </p>
    </header>

    <main class="main-content">
      <!-- 错误提示 -->
      <div v-if="store.globalError && !store.isChecking" class="error-section">
        <p class="error-title">❌ 无法获取更新信息</p>
        <p class="error-details">{{ store.globalError }}</p>
        <button @click="store.initialize()" class="button-secondary">重试</button>
      </div>

      <!-- 组件列表 -->
      <div class="component-lists">
        <!-- 核心组件 -->
        <section class="component-section">
          <h2>核心组件</h2>
          <div v-if="store.isChecking && store.coreComponents.length === 0" class="loading-placeholder">正在检查...</div>
          <ul v-else class="component-list">
            <ComponentItem
                v-for="component in store.coreComponents"
                :key="component.id"
                :component="component"
            />
          </ul>
        </section>

        <!-- 插件 -->
        <section class="component-section">
          <h2>插件</h2>
          <div v-if="store.isChecking && store.pluginComponents.length === 0" class="loading-placeholder">正在检查...</div>
          <ul v-else-if="store.pluginComponents.length > 0" class="component-list">
            <ComponentItem
                v-for="plugin in store.pluginComponents"
                :key="plugin.id"
                :component="plugin"
            />
          </ul>
          <div v-else class="loading-placeholder">没有发现可用的插件。</div>
        </section>
      </div>

    </main>

    <footer>
      <div class="footer-actions">
        <button
            v-if="store.availableUpdates.length > 0"
            @click="store.downloadAll()"
            :disabled="store.isInstalling"
            class="button-update-all"
        >
          全部更新 ({{ store.availableUpdates.length }})
        </button>
        <button @click="store.initialize()" :disabled="store.isInstalling || store.isChecking" class="button-secondary">
          刷新状态
        </button>
        <button @click="launchApp" :disabled="store.isInstalling" class="button-launch">
          启动应用
        </button>
      </div>
      <p v-if="store.availableUpdates.length > 0" class="update-hint">
        建议先完成所有更新再启动应用。
      </p>
    </footer>
  </div>
</template>

<style>
/* ... 你的 App.vue 中已有的样式，可以完全保留，无需修改 ... */
:global(html), :global(body) {
  margin: 0;
  padding: 0;
  height: 100%;
  overflow: hidden; /* 对于固定窗口大小的应用，直接禁止滚动条更稳妥 */
}

/* 保持大部分原有样式，并增加新UI所需的样式 */
.launcher-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  padding: 1.5rem;
  box-sizing: border-box;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  text-align: center;
  background-color: #f7f9fc;
}

header {
  margin-bottom: 1.5rem;
}

h1 {
  font-size: 1.8rem;
  color: #333;
}

.status-message {
  min-height: 1.5em;
  transition: color 0.3s;
  color: #666;
}

.status-message.is-busy {
  color: #007bff;
}

.status-message.is-error {
  color: #dc3545;
}

.main-content {
  flex-grow: 1;
  overflow-y: auto; /* 让组件列表区域可以滚动 */
  padding: 0 1rem;
}

.progress-section {
  margin-bottom: 2rem;
}

.progress-text {
  margin-left: 1em;
  font-size: 0.9em;
  color: #555;
}

.error-section {
  background-color: #fff3f3;
  color: #721c24;
  border: 1px solid #f5c6cb;
  padding: 1rem;
  border-radius: 8px;
  max-width: 600px;
  margin: 1rem auto;
}

.error-title {
  font-weight: bold;
  margin-top: 0;
}

.error-details {
  font-family: monospace;
  font-size: 0.9em;
  word-break: break-all;
}

.error-section button {
  margin-top: 1rem;
}

.component-lists {
  max-width: 800px;
  margin: 0 auto;
  text-align: left;
}

.component-section {
  background-color: #fff;
  border-radius: 8px;
  padding: 1.5rem;
  margin-bottom: 1.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.component-section h2 {
  margin-top: 0;
  border-bottom: 1px solid #eee;
  padding-bottom: 0.75rem;
  margin-bottom: 1rem;
  font-size: 1.2rem;
  color: #333;
}

.loading-placeholder {
  color: #888;
  padding: 1rem 0;
  text-align: center;
}

.component-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

footer {
  padding-top: 1.5rem;
  border-top: 1px solid #e0e0e0;
  background-color: #f7f9fc;
}

.footer-actions {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
}

.button-primary, .button-secondary, .button-launch, .button-update-all {
  padding: 0.6em 1.2em;
  border-radius: 6px;
  border: 1px solid transparent;
  cursor: pointer;
  font-weight: 500;
  transition: background-color 0.2s, opacity 0.2s;
}

.button-primary {
  background-color: #007bff;
  color: white;
  border-color: #007bff;
}

.button-primary:hover {
  background-color: #0056b3;
}

.button-secondary {
  background-color: #6c757d;
  color: white;
  border-color: #6c757d;
}

.button-secondary:hover {
  background-color: #5a6268;
}

.button-update-all {
  background-color: #28a745;
  color: white;
  border-color: #28a745;
}

.button-update-all:hover {
  background-color: #218838;
}

.button-launch {
  padding: 0.8rem 2.5rem;
  font-size: 1.1rem;
  background-color: #17a2b8;
  color: white;
  border-color: #17a2b8;
}

.button-launch:hover {
  background-color: #138496;
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.update-hint {
  width: 100%;
  margin-top: 1rem;
  color: #888;
  font-size: 0.9em;
}
</style>