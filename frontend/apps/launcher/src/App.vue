<!-- src/App.vue -->
<script setup lang="ts">
import {computed, onMounted, onUnmounted, ref} from 'vue';
import {invoke} from '@tauri-apps/api/core';
import {listen, type UnlistenFn} from '@tauri-apps/api/event';

// --- 1. State Management (状态管理) ---
const statusMessage = ref('Launcher is ready.');
const isDownloading = ref(false);
const downloadProgress = ref({downloaded: 0, total: 0});
let unlisten: UnlistenFn | null = null;

// --- 2. Computed Properties (计算属性) ---
// 自动计算百分比和格式化文本，模板会更干净
const progressPercentage = computed(() => {
  if (!downloadProgress.value.total) return 0;
  return Math.round((downloadProgress.value.downloaded / downloadProgress.value.total) * 100);
});

const progressText = computed(() => {
  if (!isDownloading.value) return '';
  if (downloadProgress.value.total) {
    return `${progressPercentage.value}% - ${formatBytes(downloadProgress.value.downloaded)} / ${formatBytes(downloadProgress.value.total)}`;
  }
  return `${formatBytes(downloadProgress.value.downloaded)} downloaded`;
});

// --- 3. Methods (方法) ---
async function startDownload() {
  // 重置状态
  isDownloading.value = true;
  statusMessage.value = 'Starting download...';
  downloadProgress.value = {downloaded: 0, total: 0};

  try {
    const downloadUrl = "https://speed.cloudflare.com/__down?during=download&bytes=104857600";
    const relativeSavePath = "downloads/app.zip";

    await invoke("download_file", {
      url: downloadUrl,
      relativePath: relativeSavePath,
    });

    statusMessage.value = `Download complete! Saved to ${relativeSavePath}`;
  } catch (error) {
    console.error("Download failed:", error);
    statusMessage.value = `Download failed: ${String(error)}`;
  } finally {
    isDownloading.value = false;
  }
}

// --- 4. Lifecycle Hooks (生命周期钩子) ---
onMounted(async () => {
  unlisten = await listen<{ downloaded: number; total: number | null }>(
      'download-progress',
      (event) => {
        downloadProgress.value = {
          downloaded: event.payload.downloaded,
          total: event.payload.total || 0,
        };
      }
  );
});

onUnmounted(() => {
  // 组件销毁时，清理事件监听，防止内存泄漏
  if (unlisten) {
    unlisten();
  }
});

// --- Helper Function ---
function formatBytes(bytes: number, decimals = 2): string {
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}
</script>

<template>
  <div class="container">
    <h1>My Awesome App Launcher</h1>
    <p>{{ statusMessage }}</p>

    <div class="controls">
      <button @click="startDownload" :disabled="isDownloading">
        {{ isDownloading ? 'Downloading...' : 'Download Core App (ZIP)' }}
      </button>
    </div>

    <div v-if="isDownloading" class="progress-container">
      <div class="progress-bar" :style="{ width: progressPercentage + '%' }"></div>
    </div>
    <p v-if="isDownloading">{{ progressText }}</p>
  </div>
</template>

<style scoped>
/* 把你的 CSS 样式粘贴到这里 */
.container {
  margin: 0;
  padding-top: 10vh;
  display: flex;
  flex-direction: column;
  justify-content: center;
  text-align: center;
}

.progress-container {
  width: 80%;
  max-width: 400px;
  margin: 20px auto;
  background-color: #333;
  border-radius: 5px;
  border: 1px solid #555;
}

.progress-bar {
  height: 20px;
  background-color: #4caf50;
  border-radius: 5px;
  transition: width 0.1s linear;
}

p {
  font-size: 0.9em;
  color: #ccc;
}

button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>