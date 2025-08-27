<script setup lang="ts">
import {computed, onMounted, ref, watch} from 'vue';
import {invoke} from '@tauri-apps/api/core';
import ComponentItem from './components/ComponentItem.vue';
import {useUpdaterStore} from "./stores/updaterStore.ts";
import {type ManifestMode, useConfigStore} from "./stores/configStore.ts";
import ConfirmationDialog from "./components/ConfirmationDialog.vue";

const updaterStore = useUpdaterStore();
const configStore = useConfigStore();

const frontendPath = 'wwwroot';
const backendExePath = 'backend/YAESandBox.AppWeb.exe';

const customManifestUrl = ref('');

onMounted(async () => {
  await updaterStore.listenForProgress();
  await configStore.loadConfig();
  await updaterStore.initialize();
});

// 监视 configStore 的变化，以更新本地的自定义 URL 输入框
watch(() => configStore.parsedConfig?.core_components_manifest_url, (newUrl) => {
  if (configStore.currentMode === 'custom') {
    customManifestUrl.value = newUrl || '';
  }
}, {immediate: true});

const showSlimWarningDialog = ref(false);
const slimWarningMessage = `您选择的“精简版”不包含 .NET 运行环境。

请确保您的系统已安装【.NET 9 (或更高版本) ASP.NET Core 运行时】，否则后端服务将无法启动。

您可以从微软官方网站下载：
<a href="https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0" target="_blank">https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0</a>`;

const selectedMode = computed<ManifestMode>({
  get() {
    return configStore.currentMode;
  },
  set(newMode: ManifestMode) {
    if (newMode === 'slim') {
      // 2. 显示我们的自定义对话框，而不是调用 confirm()
      showSlimWarningDialog.value = true;
    }

    if (newMode === 'full' || newMode === 'slim') {
      configStore.changeManifestUrl(configStore.MANIFEST_URLS[newMode]);
    }
    // "custom" 模式的 URL 将通过输入框和按钮单独处理
  }
});

// 处理对话框的确认事件
function handleSlimConfirm() {
  configStore.changeManifestUrl(configStore.MANIFEST_URLS.slim);
  showSlimWarningDialog.value = false;
}

// 处理对话框的取消事件
function handleSlimCancel() {
  showSlimWarningDialog.value = false;
  // selectedMode 会因为 configStore.currentMode 没变而自动弹回原来的值，无需手动处理
}


function applyCustomUrl() {
  if (customManifestUrl.value.trim()) {
    configStore.changeManifestUrl(customManifestUrl.value.trim());
  } else {
    alert('自定义 URL 不能为空。');
  }
}

const launchApp = async () => {
  // 检查是否有核心组件的更新未完成
  const hasCoreUpdates = updaterStore.coreComponents.some(c => c.status === 'update_available');
  if (hasCoreUpdates) {
    const confirmed = confirm(
        `检测到核心组件有可用更新。\n\n` +
        `在未更新的情况下启动可能会导致应用功能异常或不稳定。\n\n` +
        `您确定要继续启动吗？`
    );
    if (!confirmed) return;
  }

  updaterStore.globalStatusMessage = '正在启动本地服务...';
  try {
    await invoke('start_local_backend', {
      frontendRelativePath: frontendPath,
      backendExeRelativePath: backendExePath,
    });
    updaterStore.globalStatusMessage = '启动命令已发送。';
  } catch (error) {
    console.error('启动服务失败:', error);
    updaterStore.globalStatusMessage = `启动失败: ${String(error)}`;
  }
};

async function handleRefresh() {
  await configStore.loadConfig();
  await updaterStore.initialize();
}
</script>

<template>
  <div class="launcher-container">
    <header>
      <h1>YAESandBox 启动器</h1>
      <p class="status-message"
         :class="{ 'is-busy': updaterStore.isInstalling || updaterStore.isDownloading, 'is-error': !!updaterStore.globalError }">
        {{ updaterStore.globalStatusMessage }}
      </p>
    </header>

    <main class="main-content">
      <!-- 错误提示 -->
      <div v-if="updaterStore.globalError && !updaterStore.isChecking" class="error-section">
        <p class="error-title">❌ 无法获取更新信息</p>
        <p class="error-details">{{ updaterStore.globalError }}</p>
        <button @click="updaterStore.initialize()" class="button-secondary">重试</button>
      </div>

      <!-- 组件列表 -->
      <div class="component-lists">
        <!-- 核心组件 -->
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

        <!-- 插件 -->
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

    <footer>
      <div class="settings-area">
        <label for="manifest-mode">更新源模式:</label>
        <select id="manifest-mode" v-model="selectedMode">
          <option value="full">完整版 (自带.NET9.0环境)</option>
          <option value="slim">精简版 (需自行安装.NET9.0)</option>
          <option value="custom">自定义</option>
        </select>
        <div v-if="selectedMode === 'custom'" class="custom-url-input">
          <input type="text" v-model="customManifestUrl" placeholder="输入核心组件清单URL">
          <button @click="applyCustomUrl" class="button-secondary">应用</button>
        </div>
      </div>

      <div class="footer-actions">
        <button
            v-if="updaterStore.availableUpdates.length > 0"
            @click="updaterStore.downloadAll()"
            :disabled="updaterStore.isInstalling"
            class="button-update-all"
        >
          全部更新 ({{ updaterStore.availableUpdates.length }})
        </button>
        <button @click="updaterStore.initialize()" :disabled="updaterStore.isInstalling || updaterStore.isChecking"
                class="button-secondary">
          刷新状态
        </button>
        <button @click="launchApp" :disabled="updaterStore.isInstalling" class="button-launch">
          启动应用
        </button>
      </div>
      <p v-if="updaterStore.availableUpdates.length > 0" class="update-hint">
        建议先完成所有更新再启动应用。
      </p>
    </footer>

    <ConfirmationDialog
        v-if="showSlimWarningDialog"
        title="切换模式警告"
        :message="slimWarningMessage"
        @confirm="handleSlimConfirm"
        @cancel="handleSlimCancel"
    />
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

.custom-url-input {
  display: flex;
  gap: 0.5rem;
}

.custom-url-input input {
  width: 300px; /* 根据需要调整 */
}
</style>