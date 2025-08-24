<!-- src/App.vue -->
<template>
  <div class="container">
    <h1>My Awesome App Launcher</h1>
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
          :disabled="isDownloading"
      >
        <!-- 当某个任务正在下载时，显示特定文本 -->
        <span v-if="isDownloading && currentlyDownloadingId === item.id">Downloading...</span>
        <span v-else>Download {{ item.name }}</span>
      </button>

      <button @click="launchApp" :disabled="isDownloading">
        Launch Application
      </button>
    </div>

    <DownloadProgressBar
        v-if="isDownloading"
        :percentage="progressPercentage"
        :text="progressText"
    />

  </div>
</template>

<script setup lang="ts">
import {ref} from 'vue';
import { invoke } from '@tauri-apps/api/core';
import DownloadProgressBar from './components/DownloadProgressBar.vue';
import {type DownloadableItem, useDownloader} from './composables/useDownloader';

const frontendPath = ref('app/wwwroot');
const backendPath = ref('app/YAESandBox.AppWeb.exe');

const downloadableItems = ref<DownloadableItem[]>([
  {
    id: 'app',
    name: 'Core Application',
    url: `https://disk.sample.cat/samples/zip/sample-1.zip`,
    savePath: 'downloads/app.zip',
    extractPath: 'app/wwwroot',
  },
  {
    id: 'backend',
    name: '.NET Backend',
    url: `https://disk.sample.cat/samples/zip/sample-2.zip`,
    savePath: 'downloads/backend.zip',
    extractPath: 'app',
  },
]);

const {
  statusMessage,
  isDownloading,
  currentlyDownloadingId,
  progressPercentage,
  progressText,
  performUpdate,
} = useDownloader();

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
</style>