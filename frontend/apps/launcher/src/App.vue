<script setup lang="ts">
import {computed, onMounted, ref} from 'vue';
import {invoke} from '@tauri-apps/api/core';

// --- 1. 导入所有需要的模块 ---
import {useConfig} from './composables/useConfig';
import {type UpdateTask, useTaskManager} from './composables/useTaskManager';
import {type ComponentConfig, type RemoteCoreComponent, useCoreUpdater} from './composables/useCoreUpdater';
import {type RemotePluginInfo, usePlugins} from './composables/usePlugins';
import semver from "semver";

// --- 2. 定义应用的“单一真相源” ---

// 核心组件的配置，告诉 useCoreUpdater 如何处理它们
const CORE_COMPONENTS_CONFIG: ComponentConfig[] = [
  {id: 'app', extractPath: 'app/wwwroot'},
  {id: 'backend', extractPath: 'app'},
];

// 启动应用的路径，与更新逻辑完全解耦
const frontendPath = ref('app/wwwroot');
const backendExePath = ref('app/YAESandBox.AppWeb.exe');

// --- 3. 初始化所有 Composables ---
const {config, reloadConfig, error: configError} = useConfig();
const {executeTask, isBusy: isTaskExecutorBusy, statusMessage: taskStatus, progress, currentTask} = useTaskManager();
const {coreUpdateTasks, checkCoreUpdates, isCheckingCore, allCoreComponents, coreCheckError} = useCoreUpdater();
const {pluginUpdateTasks, checkPluginUpdates, isCheckingPlugins, allPlugins, pluginCheckError} = usePlugins();

// --- 4. 聚合状态 ---
const showManualDownloader = ref(false);

const checkError = computed(() => {
  // 优先显示更具体的错误信息
  if (coreCheckError.value) return coreCheckError.value;
  if (pluginCheckError.value) return pluginCheckError.value;
  return null;
});

// 主状态消息，优先显示任务执行器的状态，否则显示通用状态
const mainStatusMessage = ref('启动器正在初始化...');
// 全局的加载/繁忙状态，任何检查或执行任务都会使其为 true
const isGloballyBusy = computed(() => isTaskExecutorBusy.value || isCheckingCore.value || isCheckingPlugins.value);
// 所有可用更新任务的聚合列表
const allAvailableTasks = computed(() => [...coreUpdateTasks.value, ...pluginUpdateTasks.value]);

const allManifestComponents = computed(() => [...allCoreComponents.value, ...allPlugins.value]);

// --- 5. 编排生命周期和更新流程 ---

onMounted(async () => {
  mainStatusMessage.value = '正在加载配置...';
  if (!(await reloadConfig())) {
    mainStatusMessage.value = `错误: ${configError.value || '无法加载配置文件，请检查后重启。'}`;
    return;
  }

  // 步骤 A: 检查启动器更新（最高优先级）
  // 这个过程是独立的，成功则重启，失败则继续
  const launcherUpdateAvailable = await checkLauncherUpdate();
  if (launcherUpdateAvailable) {
    // 如果启动器有更新并且用户确认了，应用会重启，后续代码不执行
    // 如果用户取消，则会继续执行下面的检查
    return;
  }

  // 步骤 B: 并行检查核心组件和插件的更新
  mainStatusMessage.value = '正在检查组件和插件更新...';
  await Promise.all([
    checkCoreUpdates(CORE_COMPONENTS_CONFIG),
    checkPluginUpdates(),
  ]);

  if (allAvailableTasks.value.length > 0) {
    mainStatusMessage.value = `发现 ${allAvailableTasks.value.length} 个可用更新。`;
  } else {
    mainStatusMessage.value = '所有组件都已是最新版本。';
  }
});

/**
 * 专门处理启动器更新的检查和执行流程。
 * (这是我们的核心修改区域)
 * @returns {Promise<boolean>} 如果发现并处理了更新（用户点击了确认或取消），返回 true。
 */
async function checkLauncherUpdate(): Promise<boolean> {
  try {
    mainStatusMessage.value = '正在检查启动器更新...';

    // 步骤 1: 获取本地启动器版本和远程清单
    const [localVersions, remoteManifest] = await Promise.all([
      invoke<Record<string, string | null>>('get_local_versions'),
      invoke<{ components: RemoteCoreComponent[] }>('fetch_manifest', {
        url: config.value?.core_components_manifest_url,
        proxy: config.value?.proxy_address,
      })
    ]);

    // 步骤 2: 从清单中查找启动器信息
    const launcherInfo = remoteManifest.components.find(c => c.id === 'launcher');
    const localLauncherVersion = localVersions.launcher;

    // 步骤 3: 比较版本
    if (launcherInfo && localLauncherVersion && semver.gt(launcherInfo.version, localLauncherVersion)) {
      // 发现新版本！
      const userConfirmed = confirm(
          `发现启动器新版本 v${launcherInfo.version}！(当前 v${localLauncherVersion})\n` +
          `这是关键更新，建议立即安装。\n\n` +
          `更新日志:\n${launcherInfo.notes || '无'}\n\n` +
          `应用将会重启，是否立即更新？`
      );

      if (userConfirmed) {
        mainStatusMessage.value = '正在更新启动器，应用即将重启...';
        await invoke('apply_launcher_self_update', {
          url: launcherInfo.url,
          hash: launcherInfo.hash,
          proxy: config.value?.proxy_address,
        });
        // 如果成功，应用已退出，这里的代码不会执行
      } else {
        mainStatusMessage.value = '已取消启动器更新。请注意，部分功能可能需要新版启动器。';
      }
      return true; // 无论用户确认还是取消，我们都“处理”了更新流程
    }

    console.log('启动器已是最新版本。');
    return false; // 没有发现更新

  } catch (e) {
    const errorMsg = `检查启动器更新失败: ${String(e)}`;
    console.error(errorMsg, e);
    mainStatusMessage.value = errorMsg;
    return false; // 检查过程出错
  }
}

// --- 6. UI 交互方法 ---

// 一个可重用的函数，用于执行所有检查，方便重试
async function performUpdateChecks() {
  mainStatusMessage.value = '正在检查组件和插件更新...';
  // Promise.all 会在任何一个 promise reject 时立即失败，这正是我们想要的
  await Promise.all([
    checkCoreUpdates(CORE_COMPONENTS_CONFIG),
    checkPluginUpdates(),
  ]);

  // 更新检查完成后的状态提示逻辑
  if (checkError.value) {
    // 如果有错误，主状态就提示错误
    mainStatusMessage.value = '检查更新时发生错误。';
  } else if (allAvailableTasks.value.length > 0) {
    mainStatusMessage.value = `发现 ${allAvailableTasks.value.length} 个可用更新。`;
  } else {
    mainStatusMessage.value = '所有组件都已是最新版本。';
  }
}

/**
 * 手动下载/重新安装一个组件
 * @param component - 从 allManifestComponents 列表中选中的组件
 */
async function manualDownload(component: RemoteCoreComponent | RemotePluginInfo) {
  if (isTaskExecutorBusy.value) {
    alert('请等待当前任务执行完毕。');
    return;
  }

  if (!confirm(`确定要手动下载/重新安装 ${component.name} v${component.version} 吗？\n此操作会覆盖现有文件。`)) {
    return;
  }

  // 1. 确定解压路径
  let extractPath = '';
  if (component.id === 'launcher') {
    // 特殊情况：启动器下载到单独目录，避免覆盖自身
    extractPath = `manual_downloads/launcher_v${component.version}`;
  } else {
    const coreConfig = CORE_COMPONENTS_CONFIG.find(c => c.id === component.id);
    if (coreConfig) {
      // 是核心组件
      extractPath = coreConfig.extractPath;
    } else {
      // 默认是插件
      extractPath = `Plugins/${component.id}`;
    }
  }

  // 2. 创建一个 UpdateTask
  const task: UpdateTask = {
    ...component,
    name: `(手动) ${component.name}`, // 加个前缀以区分
    extractPath,
  };

  // 3. 执行任务
  showManualDownloader.value = false; // 先关闭模态框
  const success = await executeTask(task);

  // 4. 给出反馈
  if (success) {
    if (component.id === 'launcher') {
      alert(`新版启动器已成功下载至应用目录下的 'manual_downloads/launcher_v${component.version}' 文件夹中。\n请关闭当前启动器，并运行新版本。`);
    } else {
      alert(`${component.name} 已成功安装！`);
    }
  } else {
    alert(`${component.name} 安装失败，请查看日志获取详细信息。`);
  }
}

/**
 * 串行执行所有可用的更新任务。
 */
async function installAllUpdates() {
  for (const task of allAvailableTasks.value) {
    const success = await executeTask(task);
    if (!success) {
      alert(`更新 ${task.name} 失败，请查看日志。更新流程已中止。`);
      break; // 一旦有任务失败，就停止后续所有任务
    }
  }
  // 成功完成后，重新检查以刷新列表
  if (!isTaskExecutorBusy.value) { // 仅在没有任务失败时刷新
    mainStatusMessage.value = '所有更新已完成，正在重新校验...';
    await Promise.all([
      checkCoreUpdates(CORE_COMPONENTS_CONFIG),
      checkPluginUpdates(),
    ]);
    mainStatusMessage.value = '校验完成，所有组件都已是最新版本。';
  }
}

/**
 * 启动主应用程序。
 */
const launchApp = async () => {
  mainStatusMessage.value = '正在启动本地服务...';
  try {
    await invoke('start_local_backend', {
      frontendRelativePath: frontendPath.value,
      backendExeRelativePath: backendExePath.value,
    });
    // 这个 invoke 不会返回，因为它会打开一个新窗口并可能关闭启动器
    // 如果它返回了，说明可能出错了
    mainStatusMessage.value = '启动命令已发送。';
  } catch (error) {
    console.error('启动服务失败:', error);
    mainStatusMessage.value = `启动失败: ${String(error)}`;
  }
};
</script>

<template>
  <div class="launcher-container">
    <header>
      <h1>YAESandBox 启动器</h1>
      <p class="status-message" :class="{ 'is-busy': isGloballyBusy }">
        {{ isTaskExecutorBusy ? taskStatus : mainStatusMessage }}
      </p>
    </header>

    <main>
      <!-- 任务执行时的进度条 -->
      <div v-if="isTaskExecutorBusy && currentTask" class="progress-section">
        <p>正在处理: <strong>{{ currentTask.name }} (v{{ currentTask.version }})</strong></p>
        <progress :value="progress.percentage" max="100"></progress>
        <span class="progress-text">{{ progress.text }}</span>
      </div>

      <!-- 可用更新列表 -->
      <div v-if="allAvailableTasks.length > 0 && !isTaskExecutorBusy" class="update-section">
        <h2>发现 {{ allAvailableTasks.length }} 个可用更新</h2>
        <ul class="task-list">
          <li v-for="task in allAvailableTasks" :key="task.id">
            <div class="task-info">
              <span class="task-name">{{ task.name }}</span>
              <span class="task-version">-> v{{ task.version }}</span>
            </div>
            <button @click="executeTask(task)" :disabled="isGloballyBusy" class="button-secondary">
              单独更新
            </button>
          </li>
        </ul>
        <button @click="installAllUpdates" :disabled="isGloballyBusy" class="button-primary">
          全部更新
        </button>
      </div>

      <div v-if="checkError && !isGloballyBusy" class="error-section">
        <p class="error-title">❌ 无法获取更新信息</p>
        <p class="error-details">{{ checkError }}</p>
        <button @click="performUpdateChecks" class="button-secondary">重试</button>
      </div>

      <div v-if="allAvailableTasks.length === 0 && !isGloballyBusy && !checkError" class="no-updates">
        <p>✅ 所有组件都已是最新版本。</p>
      </div>
    </main>

    <footer>
      <button @click="launchApp" :disabled="isGloballyBusy || allAvailableTasks.length > 0" class="button-launch">
        启动应用
      </button>
      <button @click="showManualDownloader = true" :disabled="isGloballyBusy" class="button-secondary"
              style="margin-left: 1rem;">
        手动下载
      </button>
      <p v-if="allAvailableTasks.length > 0" class="update-hint">
        建议先完成所有更新再启动应用。
      </p>
    </footer>

    <div v-if="showManualDownloader" class="modal-overlay">
      <div class="modal-content">
        <header class="modal-header">
          <h2>手动下载/重新安装</h2>
          <button @click="showManualDownloader = false" class="close-button">&times;</button>
        </header>
        <div class="modal-body">
          <p class="modal-description">
            在这里您可以查看所有可用的组件和插件，并选择手动下载或重新安装特定版本。
          </p>
          <ul class="task-list manual-download-list">
            <li v-for="component in allManifestComponents" :key="component.id">
              <div class="task-info">
                <span class="task-name">{{ component.name }}</span>
                <span class="task-version">v{{ component.version }}</span>
              </div>
              <button @click="manualDownload(component)" :disabled="isTaskExecutorBusy" class="button-primary">
                下载
              </button>
            </li>
          </ul>
        </div>
      </div>
    </div>

  </div>
</template>

<style scoped>
/* 一些基础样式，让界面更清晰 */
.launcher-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  padding: 2rem;
  box-sizing: border-box;
  font-family: sans-serif;
  text-align: center;
}

header {
  margin-bottom: 2rem;
}

.status-message {
  min-height: 1.5em;
  transition: color 0.3s;
}

.status-message.is-busy {
  color: #007bff;
}

main {
  flex-grow: 1;
}

.progress-section, .update-section {
  margin-bottom: 2rem;
}

.progress-text {
  margin-left: 1em;
  font-size: 0.9em;
  color: #555;
}

.task-list {
  list-style: none;
  padding: 0;
  margin: 1rem 0;
  max-width: 500px;
  margin-left: auto;
  margin-right: auto;
}

.task-list li {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem;
  border-bottom: 1px solid #eee;
}

.task-name {
  font-weight: bold;
}

.task-version {
  color: #28a745;
  margin-left: 1em;
}

footer {
  margin-top: auto;
  display: flex; /* 新增flex布局以便排列按钮 */
  justify-content: center; /* 居中 */
  align-items: center; /* 垂直居中 */
  flex-direction: column; /* 保持原有堆叠 */
}

/* 调整按钮组为水平排列 */
footer > button {
  margin-bottom: 1rem;
}

footer {
  flex-direction: row;
  flex-wrap: wrap;
}

.button-launch {
  padding: 1rem 2rem;
  font-size: 1.2rem;
  cursor: pointer;
}

.button-launch:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.update-hint {
  width: 100%; /* 提示信息占满一行 */
  margin-top: 1rem;
  color: #888;
  font-size: 0.9em;
}

.button-primary, .button-secondary {
  padding: 0.5em 1em;
  border-radius: 4px;
  border: 1px solid transparent;
  cursor: pointer;
}

.button-primary {
  background-color: #007bff;
  color: white;
}

.button-secondary {
  background-color: #f0f0f0;
}

/* 新增：模态框样式 */
.modal-overlay {
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

.modal-content {
  background: white;
  padding: 1.5rem;
  border-radius: 8px;
  width: 90%;
  max-width: 600px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid #ddd;
  padding-bottom: 1rem;
  margin-bottom: 1rem;
}

.close-button {
  background: none;
  border: none;
  font-size: 2rem;
  cursor: pointer;
  line-height: 1;
}

.modal-body {
  overflow-y: auto; /* 使列表可滚动 */
}

.modal-description {
  font-size: 0.9em;
  color: #666;
  margin-bottom: 1.5rem;
  text-align: left;
}

.manual-download-list .task-version {
  color: #555; /* 手动下载列表中的版本号颜色稍作区分 */
}

.status-message.is-error {
  color: #dc3545; /* 红色，表示错误 */
}

.error-section {
  background-color: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
  padding: 1rem;
  border-radius: 4px;
  max-width: 500px;
  margin: 1rem auto;
}

.error-title {
  font-weight: bold;
  margin-top: 0;
}

.error-details {
  font-family: monospace; /* 使用等宽字体显示错误细节，更清晰 */
  font-size: 0.9em;
  word-break: break-all; /* 防止长 URL 撑破容器 */
}

.error-section button {
  margin-top: 1rem;
}
</style>