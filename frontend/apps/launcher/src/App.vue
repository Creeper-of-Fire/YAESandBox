<!-- src/App.vue -->
<template>
  <div class="container">
    <div class="header">
      <h1>YAESandBox Launcher</h1>
      <button @click="refreshAll" :disabled="isBusy" class="refresh-btn">
        <span v-if="isRefreshing">🔄</span>
        <span v-else>Refresh</span>
      </button>
    </div>

    <p>{{ statusMessage }}</p>

    <div>
      <p>Frontend Path: <strong>{{ frontendPath }}</strong></p>
      <p>Backend Path: <strong>{{ backendPath }}</strong></p>
    </div>

    <div class="controls">
      <button
          v-for="item in downloadableItems"
          :key="item.id"
          @click="performUpdate(item)"
          :disabled="isBusy"
      >
        <!-- 当某个任务正在下载时，显示特定文本 -->
        <span v-if="isDownloading && currentlyDownloadingId === item.id">Downloading...</span>
        <span v-else>Download {{ item.name }}</span>
      </button>

      <button @click="launchApp" :disabled="isBusy">
        Launch Application
      </button>
    </div>

    <DownloadProgressBar
        v-if="isDownloading"
        :percentage="progressPercentage"
        :text="progressText"
    />

    <hr />

    <h2>Available Plugins</h2>
    <div v-if="arePluginsLoading">
      <p>Loading plugins from manifest...</p>
    </div>
    <div v-else-if="pluginError">
      <p class="error-message">{{ pluginError }}</p>
    </div>
    <div v-else-if="plugins.length > 0" class="plugin-list">
      <div v-for="plugin in plugins" :key="plugin.id" class="plugin-card">
        <h3>{{ plugin.name }} <small>(v{{ plugin.version }})</small></h3>
        <p>{{ plugin.description }}</p>

        <button @click="installPlugin(plugin)" :disabled="isDownloading">
          <span v-if="isDownloading && currentlyDownloadingId === plugin.id">Installing...</span>
          <span v-else>Install</span>
        </button>

      </div>
    </div>
    <p v-else>No plugins found in the manifest.</p>

  </div>
</template>

<script setup lang="ts">
import {computed, ref, watchEffect} from 'vue';
import { invoke } from '@tauri-apps/api/core';
import DownloadProgressBar from './components/DownloadProgressBar.vue';
import {type DownloadableItem, useDownloader} from './composables/useDownloader';
import { useConfig } from './composables/useConfig';
import {type PluginInfo, usePlugins} from "#/composables/usePlugins.ts";

const frontendPath = ref('app/wwwroot');
const backendPath = ref('app/YAESandBox.AppWeb.exe');

const downloadableItems = ref<DownloadableItem[]>([
  {
    id: 'app',
    name: 'Core Application',
    url: '',
    savePath: 'downloads/app.zip',
    extractPath: 'app/wwwroot',
  },
  {
    id: 'backend',
    name: '.NET Backend',
    url: '',
    savePath: 'downloads/backend.zip',
    extractPath: 'app',
  },
]);

const { config, isLoading: isConfigLoading, error: configError,reloadConfig } = useConfig();
const { plugins, isLoading: arePluginsLoading, error:pluginError, fetchPlugins } = usePlugins();

const isRefreshing = ref(false);
const isBusy = computed(() => isDownloading.value || isRefreshing.value);

const refreshAll = async () => {
  if (isBusy.value) return; // 如果正在下载或刷新，则不执行

  isRefreshing.value = true;
  statusMessage.value = 'Refreshing configuration...';

  try {
    // 步骤 A: 重新加载本地配置
    await reloadConfig();

    // 检查配置加载是否出错
    if (configError.value) {
      statusMessage.value = `Failed to refresh config: ${configError.value}`;
      return;
    }

    // 步骤 B: 使用新配置重新加载插件列表
    statusMessage.value = 'Refreshing plugin list...';
    await fetchPlugins();

    if (pluginError.value) {
      statusMessage.value = `Failed to refresh plugins: ${pluginError.value}`;
    } else {
      statusMessage.value = 'Refresh complete. Launcher is ready.';
    }

  } finally {
    isRefreshing.value = false;
  }
};


watchEffect(() => {
  if (config.value) {
    // 配置加载成功后，更新我们的下载项
    const appItem = downloadableItems.value.find(item => item.id === 'app');
    if (appItem) appItem.url = config.value.app_download_url;

    const backendItem = downloadableItems.value.find(item => item.id === 'backend');
    if (backendItem) backendItem.url = config.value.backend_download_url;

    // 也可以在这里触发插件列表的加载
    fetchPlugins();
  }
});

const {
  statusMessage,
  isDownloading,
  currentlyDownloadingId,
  progressPercentage,
  progressText,
  performUpdate,
} = useDownloader();

const installPlugin = (plugin: PluginInfo) => {
  // 动态创建一个 DownloadableItem，这是“翻译”步骤
  const pluginItem: DownloadableItem = {
    id: plugin.id,
    name: plugin.name,
    url: plugin.url,
    savePath: `downloads/plugins/${plugin.id}.zip`,
    extractPath: `Plugins/${plugin.id}`, // 每个插件安装到独立的子目录
  };

  // 将任务交给 Downloader 去执行，这是“调度”步骤
  performUpdate(pluginItem);
};

const launchApp = async () => {
  statusMessage.value = 'Starting local services...';
  try {
    await invoke('start_local_backend', {
      frontendRelativePath: frontendPath.value,
      backendExeRelativePath: backendPath.value,
    });
    statusMessage.value = 'Navigation successful. Loading application...';
  } catch (error) {
    console.error('Failed to start services:', error);
    statusMessage.value = `Failed to start: ${String(error)}`;
  }
};

</script>

<style scoped>
/* App.vue 现在只需要容器和按钮的样式 */
.container {
  margin: 0;
  padding-top: 10vh;
  display: flex;
  flex-direction: column;
  justify-content: center;
  text-align: center;
}

.controls {
  display: flex;
  justify-content: center;
  gap: 1rem; /* 给按钮之间加点间距 */
  margin-top: 1rem;
}

button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.header {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 1rem;
}

.refresh-btn {
  padding: 0.5rem;
  background-color: transparent;
  border: 1px solid #555;
  border-radius: 4px;
  cursor: pointer;
}
.refresh-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.refresh-btn span {
  display: inline-block;
}

/* 简单的旋转动画 */
@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
.refresh-btn span:first-child {
  animation: spin 1s linear infinite;
}
</style>