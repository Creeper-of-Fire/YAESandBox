<!-- src/views/LauncherView.vue -->
<script lang="ts" setup>
import {computed, onMounted, ref, watch} from 'vue';
import {invoke} from '@tauri-apps/api/core';
import ComponentItem from '../components/ComponentItem.vue';
import {useUpdaterStore} from "../stores/updaterStore.ts";
import {type ManifestMode, useConfigStore} from "../stores/configStore.ts";
import ConfirmationDialog from "../components/ConfirmationDialog.vue";
import SpecialComponentItem from "../components/SpecialComponentItem.vue";

const updaterStore = useUpdaterStore();
const configStore = useConfigStore();

const frontendPath = 'wwwroot';
const backendExePath = 'backend/YAESandBox.AppWeb.exe';

const customManifestUrl = ref('');

onMounted(async () =>
{
  await updaterStore.listenForProgress();
  await configStore.loadConfig();
  await updaterStore.initialize();
});

// 监视 configStore 的变化，以更新本地的自定义 URL 输入框
watch(() => configStore.parsedConfig?.core_components_manifest_url, (newUrl) =>
{
  if (configStore.currentMode === 'custom')
  {
    customManifestUrl.value = newUrl || '';
  }
}, {immediate: true});

const showSlimWarningDialog = ref(false);
const slimWarningMessage = `您选择的“精简版”不包含 .NET 运行环境。

请确保您的系统已安装【.NET 9 (或更高版本) ASP.NET Core 运行时】，否则后端服务将无法启动。

您可以从微软官方网站下载：
<a href="https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0" target="_blank">https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0</a>`;

const selectedMode = computed<ManifestMode>({
  get()
  {
    return configStore.currentMode;
  },
  set(newMode: ManifestMode)
  {
    if (newMode === 'slim')
    {
      // 2. 显示我们的自定义对话框，而不是调用 confirm()
      showSlimWarningDialog.value = true;
    }

    if (newMode === 'full' || newMode === 'slim')
    {
      configStore.changeManifestUrl(configStore.MANIFEST_URLS[newMode]);
    }
    // "custom" 模式的 URL 将通过输入框和按钮单独处理
  }
});

// 处理对话框的确认事件
function handleSlimConfirm()
{
  configStore.changeManifestUrl(configStore.MANIFEST_URLS.slim);
  showSlimWarningDialog.value = false;
}

// 处理对话框的取消事件
function handleSlimCancel()
{
  showSlimWarningDialog.value = false;
  // selectedMode 会因为 configStore.currentMode 没变而自动弹回原来的值，无需手动处理
}


function applyCustomUrl()
{
  if (customManifestUrl.value.trim())
  {
    configStore.changeManifestUrl(customManifestUrl.value.trim());
  }
  else
  {
    alert('自定义 URL 不能为空。');
  }
}

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

async function handleRefresh()
{
  await configStore.loadConfig();
  await updaterStore.initialize();
}
</script>

<template>
  <div class="launcher-container">
    <!-- 左侧侧边栏 -->
    <aside class="side-panel">
      <!-- 顶部信息区 -->
      <div class="side-panel-header">
        <h1>YAESandBox 启动器</h1>
        <p :class="{ 'is-busy': updaterStore.isInstalling || updaterStore.isDownloading, 'is-error': !!updaterStore.globalError }"
           class="status-message">
          {{ updaterStore.globalStatusMessage }}
        </p>
      </div>

      <!-- 底部操作区 -->
      <div class="side-panel-footer">
        <div class="settings-area">
          <label for="manifest-mode">更新源模式:</label>
          <select id="manifest-mode" v-model="selectedMode">
            <option value="full">完整版 (自带.NET9.0环境)</option>
            <option value="slim">精简版 (需自行安装.NET9.0)</option>
            <option value="custom">自定义</option>
          </select>
          <div v-if="selectedMode === 'custom'" class="custom-url-input">
            <input v-model="customManifestUrl" placeholder="输入核心组件清单URL" type="text">
            <button class="button-secondary" @click="applyCustomUrl">应用</button>
          </div>
        </div>

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

    <ConfirmationDialog
        v-if="showSlimWarningDialog"
        :message="slimWarningMessage"
        title="切换模式警告"
        @cancel="handleSlimCancel"
        @confirm="handleSlimConfirm"
    />
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
  font-weight: 500;
  transition: background-color 0.2s, opacity 0.2s;
}

.button-primary {
  background-color: #007bff;
  color: white;
  border-color: #007bff;
}

.button-primary:hover:not(:disabled) {
  background-color: #0056b3;
}

.button-secondary {
  background-color: #6c757d;
  color: white;
  border-color: #6c757d;
}

.button-secondary:hover:not(:disabled) {
  background-color: #5a6268;
}

.button-update-all {
  background-color: #28a745;
  color: white;
  border-color: #28a745;
}

.button-update-all:hover:not(:disabled) {
  background-color: #218838;
}

.button-launch {
  padding: 0.7rem 2rem;
  font-size: 1rem;
  background-color: #17a2b8;
  color: white;
  border-color: #17a2b8;
}

.button-launch:hover:not(:disabled) {
  background-color: #138496;
}

button:disabled,
.button-primary:disabled,
.button-secondary:disabled,
.button-launch:disabled,
.button-update-all:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}
</style>

<style scoped>


/* 保持大部分原有样式，并增加新UI所需的样式 */
.launcher-container {
  display: flex;
  flex-direction: row;
  height: 100vh;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  background-color: #ffffff;
}

/* --- 左侧侧边栏 --- */
.side-panel {
  display: flex;
  flex-direction: column; /* 侧边栏内部是垂直布局 */
  flex-shrink: 0; /* 防止侧边栏被压缩 */
  width: 280px; /* 固定宽度 */
  padding: 1.5rem;
  box-sizing: border-box;
  background-color: #fff;
  border-right: 1px solid #e0e0e0;
  text-align: left;
}

.side-panel-header {
  flex-shrink: 0;
}

.side-panel-footer {
  margin-top: auto; /* 关键：将操作区推到底部 */
  flex-shrink: 0;
}

.side-panel h1 {
  font-size: 1.6rem;
  color: #333;
  margin-top: 0;
}

.side-panel .status-message {
  min-height: 2.5em; /* 留出两行空间 */
  line-height: 1.4;
  word-break: break-all;
}

/* 重新设计设置区域和操作按钮以适应侧边栏 */
.side-panel .settings-area {
  display: flex;
  flex-direction: column;
  align-items: stretch; /* 让子元素撑满宽度 */
  gap: 0.5rem;
  margin-bottom: 1.5rem;
  font-size: 0.9em;
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
  background-color: #f7f9fc;
}

.status-message.is-busy {
  color: #007bff;
}

.status-message.is-error {
  color: #dc3545;
}

.launcher-update-section {
  max-width: 800px;
  margin: 0 auto 1.5rem auto;
  border-radius: 8px;
  background-color: #fff;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  overflow: hidden;
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

.component-section:last-child {
  margin-bottom: 0;
}

.component-section h2 {
  margin-top: 0;
  border-bottom: 1px solid #eee;
  padding-bottom: 0.75rem;
  margin-bottom: 1rem;
  font-size: 1.2rem;
  color: #333;
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

.settings-area {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
  font-size: 0.9em;
  color: #555;
}

.settings-area select, .settings-area input {
  padding: 0.4rem;
  border-radius: 4px;
  border: 1px solid #ccc;
  background-color: #fff;
}
</style>