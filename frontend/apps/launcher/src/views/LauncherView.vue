<!-- src/views/LauncherView.vue -->
<script lang="ts" setup>
import {computed, onMounted, ref, watch} from 'vue';
import {invoke} from '@tauri-apps/api/core';
import ComponentItem from '../components/ComponentItem.vue';
import {useUpdaterStore} from "../stores/updaterStore";
import {type ManifestMode, useConfigStore} from "../stores/configStore";
import ConfirmationDialog from "../components/ConfirmationDialog.vue";
import SpecialComponentItem from "../components/SpecialComponentItem.vue";
import SettingsPanel from "../components/SettingsPanel.vue";

const updaterStore = useUpdaterStore();
const configStore = useConfigStore();

const frontendPath = 'wwwroot';
const backendExePath = 'backend/YAESandBox.AppWeb.exe';

onMounted(async () =>
{
  await updaterStore.listenForProgress();
  await configStore.loadConfig();
  await updaterStore.initialize();
});

const launchApp = async () =>
{
  // 检查是否有核心组件的更新未完成
  const hasCoreUpdates = updaterStore.coreComponents.some(c => c.status === 'update_available');
  if (hasCoreUpdates)
  {
    const confirmed = confirm(
        `检测到核心组件有可用更新。\n\n` +
        `在未更新的情况下启动可能会导致应用功能异常或不稳定。\n\n` +
        `您确定要继续启动吗？`
    );
    if (!confirmed) return;
  }

  updaterStore.globalStatusMessage = '正在启动本地服务...';
  try
  {
    await invoke('start_local_backend', {
      frontendRelativePath: frontendPath,
      backendExeRelativePath: backendExePath,
    });
    updaterStore.globalStatusMessage = '启动命令已发送。';
  } catch (error)
  {
    console.error('启动服务失败:', error);
    updaterStore.globalStatusMessage = `启动失败: ${String(error)}`;
  }
};
</script>

<template>
  <div class="launcher-container">
    <!-- 左侧侧边栏 -->
    <aside class="side-panel">
      <!-- 顶部信息区 -->
      <div class="side-panel-header">
        <h1>YAESandBox</h1>
        <p :class="{ 'is-busy': updaterStore.isInstalling || updaterStore.isDownloading, 'is-error': !!updaterStore.globalError }"
           class="status-message">
          {{ updaterStore.globalStatusMessage }}
        </p>
      </div>

      <div class="side-panel-body">
        <div class="settings-area">
          <SettingsPanel />
        </div>
      </div>

      <!-- 底部操作区 -->
      <div class="side-panel-footer">
        <div class="footer-actions">
          <button
              v-if="updaterStore.availableUpdates.length > 0"
              :disabled="updaterStore.isInstalling"
              class="button-update-all"
              @click="updaterStore.downloadAll()"
          >
            全部更新 ({{ updaterStore.availableUpdates.length }})
          </button>
          <button :disabled="updaterStore.isInstalling || updaterStore.isChecking" class="button-secondary"
                  @click="updaterStore.initialize()">
            刷新状态
          </button>
          <button :disabled="updaterStore.isInstalling" class="button-launch" @click="launchApp">
            启动应用
          </button>
        </div>
        <p v-if="updaterStore.availableUpdates.length > 0" class="update-hint">
          建议先完成所有更新再启动应用。
        </p>
      </div>
    </aside>

    <!-- 右侧主内容区 -->
    <main class="main-content">
      <div v-if="updaterStore.globalError && !updaterStore.isChecking" class="error-section">
        <p class="error-title">❌ 无法获取更新信息</p>
        <p class="error-details">{{ updaterStore.globalError }}</p>
        <button class="button-secondary" @click="updaterStore.initialize()">重试</button>
      </div>

      <section v-if="updaterStore.launcherComponent" class="launcher-update-section">
        <SpecialComponentItem :component="updaterStore.launcherComponent"/>
      </section>

      <div class="component-lists">
        <section class="component-section">
          <h2>核心组件</h2>
          <div v-if="updaterStore.isChecking && updaterStore.coreComponents.length === 0" class="loading-placeholder">
            正在检查...
          </div>
          <ul v-else class="component-list">
            <ComponentItem
                v-for="component in updaterStore.coreComponents"
                :key="component.id"
                :component="component"
            />
          </ul>
        </section>

        <section class="component-section">
          <h2>插件</h2>
          <div v-if="updaterStore.isChecking && updaterStore.pluginComponents.length === 0" class="loading-placeholder">
            正在检查...
          </div>
          <ul v-else-if="updaterStore.pluginComponents.length > 0" class="component-list">
            <ComponentItem
                v-for="plugin in updaterStore.pluginComponents"
                :key="plugin.id"
                :component="plugin"
            />
          </ul>
          <div v-else class="loading-placeholder">没有发现可用的插件。</div>
        </section>
      </div>
    </main>
  </div>
</template>

<!-- 1. 全局样式 (不带 scoped) -->
<style>
:global(html), :global(body) {
  margin: 0;
  padding: 0;
  height: 100%;
  overflow: hidden; /* 对于固定窗口大小的应用，直接禁止滚动条更稳妥 */
}

.button-primary,
.button-secondary,
.button-launch,
.button-update-all {
  padding: 0.6em 1.2em;
  border-radius: 6px;
  border: 1px solid transparent;
  cursor: pointer;
  font-weight: 600;
  transition: all 0.2s ease-in-out;
  box-shadow: 0 1px 2px 0 rgba(0,0,0,0.05);
}

.button-primary,.button-secondary .button-launch, .button-update-all {
  color: var(--text-color-inverted);
}

.button-primary {
  background-color: var(--color-brand);
  border-color: var(--color-brand);
}

.button-primary:hover:not(:disabled) {
  background-color: var(--color-brand-hover);
  transform: translateY(-1px);
  box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1), 0 2px 4px -2px rgba(0,0,0,0.1);
}

/* Secondary Button */
.button-secondary {
  background-color: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
  border-color: var(--btn-secondary-border);
  box-shadow: none; /* Secondary buttons don't need a strong shadow */
}
.button-secondary:hover:not(:disabled) {
  background-color: var(--btn-secondary-bg-hover);
  border-color: var(--btn-secondary-bg-hover);
}

.button-update-all {
  background-color: var(--color-success);
  border-color: var(--color-success);
}
.button-update-all:hover:not(:disabled) {
  background-color: var(--color-success-hover);
  transform: translateY(-1px);
  box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1), 0 2px 4px -2px rgba(0,0,0,0.1);
}

.button-launch {
  padding: 0.8rem 2rem;
  font-size: 1.1rem;
  background-color: var(--color-info);
  border-color: var(--color-info);
}
.button-launch:hover:not(:disabled) {
  background-color: var(--color-info-hover);
  transform: translateY(-1px);
  box-shadow: 0 4px 8px -2px rgba(0,0,0,0.15), 0 2px 6px -3px rgba(0,0,0,0.15);
}

button:disabled,
.button-primary:disabled,
.button-secondary:disabled,
.button-launch:disabled,
.button-update-all:disabled {
  cursor: not-allowed;
  opacity: 0.6;
  box-shadow: none;
  transform: none;
}
</style>

<style scoped>


/* 保持大部分原有样式，并增加新UI所需的样式 */
.launcher-container {
  display: flex;
  flex-direction: row;
  height: 100vh;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  background-color: var(--bg-color-panel);
}

/* --- 左侧侧边栏 --- */
.side-panel {
  display: flex;
  flex-direction: column; /* 侧边栏内部是垂直布局 */
  flex-shrink: 0; /* 防止侧边栏被压缩 */
  width: 280px; /* 固定宽度 */
  padding: 1.5rem;
  box-sizing: border-box;
  background-color: var(--bg-color-panel);
  border-right: 1px solid var(--border-color-medium);
  text-align: left;
  color: var(--text-color-primary);
}

.side-panel-header {
  flex-shrink: 0;
}

/* 可滚动的主体区域 */
.side-panel-body {
  flex-grow: 1; /* 占据头部和底部之间的所有可用空间 */
  overflow-y: auto; /* 当内容溢出时，显示垂直滚动条 */
  min-height: 0; /* Flexbox 布局中实现滚动的关键技巧 */
}

.side-panel-footer {
  margin-top: auto; /* 关键：将操作区推到底部 */
  flex-shrink: 0;
}

.side-panel h1 {
  font-size: 1.6rem;
  color: var(--text-color-primary);
  margin-top: 0;
}

.side-panel .status-message {
  min-height: 2.5em; /* 留出两行空间 */
  line-height: 1.4;
  word-break: break-all;
}

/* 重新设计设置区域和操作按钮以适应侧边栏 */
.side-panel .settings-area {
  padding: 1rem;
  border: 1px solid var(--border-color-medium);
  border-radius: 8px;
  background-color: rgba(0, 0, 0, 0.02);
}

.side-panel .custom-url-input {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.side-panel .custom-url-input input {
  width: 100%;
  box-sizing: border-box;
}

.side-panel .footer-actions {
  display: flex;
  flex-direction: column; /* 按钮垂直排列 */
  gap: 0.75rem;
}

.side-panel .footer-actions button {
  width: 100%; /* 按钮撑满侧边栏 */
}

.side-panel .update-hint {
  width: 100%;
  margin-top: 1rem;
  font-size: 0.9em;
  text-align: left;
}

/* --- 右侧主内容区 --- */
.main-content {
  flex-grow: 1;
  overflow-y: auto;
  padding: 1.5rem;
  background-color: var(--bg-color-main);
}

.status-message.is-busy {
  color: var(--color-brand);
}

.status-message.is-error {
  color: var(--color-danger);
}

.launcher-update-section {
  max-width: 800px;
  margin: 0 auto 1.5rem auto;
  border-radius: 8px;
  background-color: var(--bg-color-card);
  box-shadow: var(--shadow-card);
  overflow: hidden;
}

.component-lists {
  max-width: 800px;
  margin: 0 auto;
  text-align: left;
}

.component-section {
  background-color: var(--bg-color-card);
  border-radius: 8px;
  padding: 1.5rem;
  margin-bottom: 1.5rem;
  box-shadow: var(--shadow-card);
}

.component-section:last-child {
  margin-bottom: 0;
}

.component-section h2 {
  margin-top: 0;
  border-bottom: 1px solid var(--border-color-divider);
  padding-bottom: 0.75rem;
  margin-bottom: 1rem;
  font-size: 1.2rem;
  color: var(--text-color-primary);
}

.error-section {
  background-color: var(--bg-color-danger-soft);
  color: var(--text-color-danger);
  border: 1px solid var(--border-color-danger);
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

.loading-placeholder {
  color: var(--text-color-muted);
  padding: 1rem 0;
  text-align: center;
}

.component-list {
  list-style: none;
  padding: 0;
  margin: 0;
}
</style>