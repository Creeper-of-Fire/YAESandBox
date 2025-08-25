<script setup lang="ts">
import {computed, type ComputedRef, onMounted, type Ref, ref} from 'vue';
import {invoke} from '@tauri-apps/api/core';
import semver from "semver";

// --- 1. 导入所有需要的模块 ---
import {useConfig} from './composables/useConfig';
import {type UpdateTask, useTaskManager} from './composables/useTaskManager';
import {type ComponentConfig, type RemoteCoreComponent, useCoreUpdater} from './composables/useCoreUpdater';
import {type RemotePluginInfo, usePlugins} from './composables/usePlugins';

// --- 2. 定义应用的“单一真相源” (Source of Truth) ---

// 核心组件的配置，告诉 useCoreUpdater 如何处理它们
const CORE_COMPONENTS_CONFIG: ComponentConfig[] = [
  {id: 'launcher', extractPath: 'manual_downloads/launcher_update'}, // 启动器特殊处理
  {id: 'app', extractPath: 'wwwroot'},
  {id: 'backend', extractPath: 'backend'},
];

// 启动应用的路径
const frontendPath = ref('wwwroot');
const backendExePath = ref('backend/YAESandBox.AppWeb.exe');

// 本地版本信息
const localVersions = ref<Record<string, string | null>>({});

// --- 3. 初始化所有 Composables ---
const {config, reloadConfig, error: configError} = useConfig();
const {executeTask, isBusy: isTaskExecutorBusy, statusMessage: taskStatus, progress, currentTask} = useTaskManager();
const {coreUpdateTasks, checkCoreUpdates, isCheckingCore, allCoreComponents, coreCheckError} = useCoreUpdater();
const {pluginUpdateTasks, checkPluginUpdates, isCheckingPlugins, allPlugins, pluginCheckError} = usePlugins();


// --- 4. 派生和聚合状态 (Derived and Aggregated State) ---

// 定义一个统一的组件展示类型，包含所有UI需要的信息
type DisplayComponent = (RemoteCoreComponent | RemotePluginInfo) & {
  localVersion: string | null;
  status: 'uptodate' | 'update_available' | 'not_installed';
  type: 'core' | 'plugin';
};

// 聚合所有检查错误
const checkError = computed(() => coreCheckError.value || pluginCheckError.value || null);

// 全局繁忙状态
const isGloballyBusy = computed(() => isTaskExecutorBusy.value || isCheckingCore.value || isCheckingPlugins.value);

// 主状态消息
const mainStatusMessage = ref('启动器正在初始化...');

// 【核心重构】创建用于UI渲染的计算属性列表
const createDisplayList = <T extends RemoteCoreComponent | RemotePluginInfo>(
    remoteComponents: Readonly<Ref<readonly T[]>>,
    isChecking: Ref<boolean>,
    type: 'core' | 'plugin'
): ComputedRef<DisplayComponent[]> => {
  return computed(() => {
    if (isChecking.value && remoteComponents.value.length === 0) {
      // 正在检查且还没有数据时，显示加载状态
      return []; // 或者可以返回一个带骨架屏信息的对象
    }

    return remoteComponents.value.map(remote => {
      const localVersion = localVersions.value[remote.id] || null;
      let status: DisplayComponent['status'] = 'not_installed';

      if (localVersion) {
        if (semver.gt(remote.version, localVersion)) {
          status = 'update_available';
        } else {
          status = 'uptodate';
        }
      }

      return {
        ...remote,
        localVersion,
        status,
        type,
      };
    });
  });
};

const displayedCoreComponents = createDisplayList(allCoreComponents, isCheckingCore, 'core');
const displayedPlugins = createDisplayList(allPlugins, isCheckingPlugins, 'plugin');

// 依然保留 allAvailableTasks 用于 "全部更新" 按钮
const allAvailableTasks = computed(() => [...coreUpdateTasks.value, ...pluginUpdateTasks.value]);


// --- 5. 编排生命周期和更新流程 ---

onMounted(async () => {
  mainStatusMessage.value = '正在加载配置...';
  if (!(await reloadConfig())) {
    mainStatusMessage.value = `错误: ${configError.value || '无法加载配置文件，请检查后重启。'}`;
    return;
  }

  // 【流程修复】首先检查启动器更新，但不再因为用户取消而中断整个流程
  await checkLauncherUpdate();

  // 无论启动器更新与否，都继续执行后续的检查
  await performUpdateChecks();
});

/**
 * 专门处理启动器更新的检查和执行流程。
 * @returns {Promise<void>}
 */
async function checkLauncherUpdate(): Promise<void> {
  try {
    mainStatusMessage.value = '正在检查启动器更新...';

    const localLauncherVersion = (await invoke<Record<string, string | null>>('get_local_versions'))['launcher'];
    const remoteManifest = await invoke<{ components: RemoteCoreComponent[] }>('fetch_manifest', {
      url: config.value?.core_components_manifest_url,
      proxy: config.value?.proxy_address,
    });

    const launcherInfo = remoteManifest.components.find(c => c.id === 'launcher');

    if (launcherInfo && localLauncherVersion && semver.gt(launcherInfo.version, localLauncherVersion)) {
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
          newVersion: launcherInfo.version,
        });
        // 如果成功，应用已退出，后续代码不会执行
      } else {
        mainStatusMessage.value = '已取消启动器更新。将继续检查其他组件。';
      }
    } else {
      console.log('启动器已是最新版本。');
    }

  } catch (e) {
    const errorMsg = `检查启动器更新失败: ${String(e)}`;
    console.error(errorMsg, e);
    // 不更新主状态，让后续检查的状态覆盖它
  }
}

/**
 * 可重用的函数，用于执行所有检查，方便重试
 */
async function performUpdateChecks() {
  mainStatusMessage.value = '正在获取本地版本信息...';
  try {
    localVersions.value = await invoke('get_local_versions');
  } catch (e) {
    mainStatusMessage.value = `获取本地版本失败: ${e}`;
    // coreCheckError.value = `无法读取本地版本信息，请检查应用完整性。错误: ${e}`;
    return;
  }

  mainStatusMessage.value = '正在检查组件和插件更新...';

  // 并行检查
  await Promise.all([
    checkCoreUpdates(CORE_COMPONENTS_CONFIG),
    checkPluginUpdates(),
  ]);

  // 更新检查完成后的状态提示逻辑
  if (checkError.value) {
    mainStatusMessage.value = '检查更新时发生错误。';
  } else if (allAvailableTasks.value.length > 0) {
    mainStatusMessage.value = `发现 ${allAvailableTasks.value.length} 个可用更新。`;
  } else {
    mainStatusMessage.value = '所有组件都已是最新版本。';
  }
}

// --- 6. UI 交互方法 ---

/**
 * 【核心重构】处理单个组件的安装/更新/重装操作
 * @param component - 从列表中选中的 DisplayComponent
 */
async function handleComponentAction(component: DisplayComponent) {
  if (isTaskExecutorBusy.value) {
    alert('请等待当前任务执行完毕。');
    return;
  }

  // 特殊处理启动器更新
  // 只要组件 ID 是 'launcher'，就强制走特殊的自我更新流程，无论状态是什么。
  if (component.id === 'launcher') {
    const actionText = component.status === 'update_available' ? '更新' : '重新安装';
    const userConfirmed = confirm(
        `您确定要${actionText}启动器吗？\n` +
        `此操作将会下载最新版本 v${component.version} 并重启应用。`
    );

    if (userConfirmed) {
      mainStatusMessage.value = '正在处理启动器，应用即将重启...';
      try {
        // 直接调用能重启的 Rust 命令
        await invoke('apply_launcher_self_update', {
          url: component.url,
          hash: component.hash,
          proxy: config.value?.proxy_address,
          newVersion: component.version,
        });
        // 如果成功，应用已退出，后续代码不执行
      } catch (e) {
        const errorMsg = `启动器${actionText}失败: ${String(e)}`;
        console.error(errorMsg, e);
        alert(errorMsg);
        mainStatusMessage.value = errorMsg; // 更新主状态让用户看到错误
      }
    }
    return; // 无论用户是否确认，都到此为止，不执行下面的通用逻辑
  }

  // 1. 确定解压路径
  let extractPath = '';
  const coreConfig = CORE_COMPONENTS_CONFIG.find(c => c.id === component.id);
  if (coreConfig) {
    extractPath = coreConfig.extractPath;
  } else {
    // 默认为插件
    extractPath = `Plugins/${component.id}`;
  }

  // 2. 创建一个 UpdateTask
  const task: UpdateTask = {
    id: component.id,
    name: component.name,
    version: component.version,
    url: component.url,
    hash: component.hash,
    extractPath,
  };

  // 3. 执行任务
  const success = await executeTask(task);

  // 4. 给出反馈并刷新状态
  if (success) {
    alert(`${component.name} 已成功安装/更新！`);
  } else {
    alert(`${component.name} 操作失败，请查看日志获取详细信息。`);
  }
  // 无论成功失败，都重新检查以刷新UI
  await performUpdateChecks();
}


/**
 * 串行执行所有可用的更新任务。
 */
async function installAllUpdates() {
  for (const task of allAvailableTasks.value) {
    // 启动器更新有自己的重启逻辑，不适合批量处理，在此跳过
    if (task.id === 'launcher') continue;

    const success = await executeTask(task);
    if (!success) {
      alert(`更新 ${task.name} 失败，更新流程已中止。`);
      break;
    }
  }

  // 检查是否还有启动器更新
  if (allAvailableTasks.value.some(t => t.id === 'launcher')) {
    alert('其他组件更新完毕！启动器需要单独更新，请点击其旁边的更新按钮。');
  }

  // 刷新状态
  await performUpdateChecks();
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
      <p class="status-message" :class="{ 'is-busy': isGloballyBusy, 'is-error': !!checkError }">
        {{ isTaskExecutorBusy ? taskStatus : mainStatusMessage }}
      </p>
    </header>

    <main class="main-content">
      <!-- 任务执行时的进度条 -->
      <div v-if="isTaskExecutorBusy && currentTask" class="progress-section">
        <p>正在处理: <strong>{{ currentTask.name }} (v{{ currentTask.version }})</strong></p>
        <progress :value="progress.percentage" max="100"></progress>
        <span class="progress-text">{{ progress.text }}</span>
      </div>

      <!-- 错误提示 -->
      <div v-if="checkError && !isGloballyBusy" class="error-section">
        <p class="error-title">❌ 无法获取更新信息</p>
        <p class="error-details">{{ checkError }}</p>
        <button @click="performUpdateChecks" class="button-secondary">重试</button>
      </div>

      <!-- 组件列表 -->
      <div class="component-lists" v-if="!isTaskExecutorBusy">
        <!-- 核心组件 -->
        <section class="component-section">
          <h2>核心组件</h2>
          <div v-if="isCheckingCore && displayedCoreComponents.length === 0" class="loading-placeholder">正在检查...
          </div>
          <ul v-else class="component-list">
            <li v-for="component in displayedCoreComponents" :key="component.id">
              <div class="info">
                <span class="name">{{ component.name }}</span>
                <span class="version-info">
                  <span v-if="component.localVersion" class="local-version">v{{ component.localVersion }}</span>
                  <span v-if="component.status === 'update_available'" class="arrow">→</span>
                  <span v-if="component.status === 'update_available'" class="remote-version">v{{
                      component.version
                    }}</span>
                </span>
              </div>
              <div class="status-action">
                 <span :class="['status-tag', `status-${component.status}`]">
                  {{ {uptodate: '最新', update_available: '可更新', not_installed: '未安装'}[component.status] }}
                </span>
                <button
                    @click="handleComponentAction(component)"
                    :disabled="isGloballyBusy"
                    class="button-primary"
                >
                  {{
                    component.status === 'uptodate' ? '重新安装' : (component.status === 'update_available' ? '更新' : '安装')
                  }}
                </button>
              </div>
            </li>
          </ul>
        </section>

        <!-- 插件 -->
        <section class="component-section">
          <h2>插件</h2>
          <div v-if="isCheckingPlugins && displayedPlugins.length === 0" class="loading-placeholder">正在检查...</div>
          <ul v-else-if="displayedPlugins.length > 0" class="component-list">
            <li v-for="plugin in displayedPlugins" :key="plugin.id">
              <div class="info">
                <span class="name">{{ plugin.name }}</span>
                <span class="version-info">
                  <span v-if="plugin.localVersion" class="local-version">v{{ plugin.localVersion }}</span>
                  <span v-if="plugin.status === 'update_available'" class="arrow">→</span>
                  <span v-if="plugin.status === 'update_available'" class="remote-version">v{{ plugin.version }}</span>
                </span>
              </div>
              <div class="status-action">
                <span :class="['status-tag', `status-${plugin.status}`]">
                  {{ {uptodate: '最新', update_available: '可更新', not_installed: '未安装'}[plugin.status] }}
                </span>
                <button
                    @click="handleComponentAction(plugin)"
                    :disabled="isGloballyBusy"
                    class="button-primary"
                >
                  {{
                    plugin.status === 'uptodate' ? '重新安装' : (plugin.status === 'update_available' ? '更新' : '安装')
                  }}
                </button>
              </div>
            </li>
          </ul>
          <div v-else class="loading-placeholder">没有发现可用的插件。</div>
        </section>
      </div>

    </main>

    <footer>
      <div class="footer-actions">
        <button
            v-if="allAvailableTasks.length > 0"
            @click="installAllUpdates"
            :disabled="isGloballyBusy"
            class="button-update-all"
        >
          全部更新 ({{ allAvailableTasks.length }})
        </button>
        <button @click="performUpdateChecks" :disabled="isGloballyBusy" class="button-secondary">
          刷新状态
        </button>
        <button @click="launchApp" :disabled="isGloballyBusy || allAvailableTasks.length > 0" class="button-launch">
          启动应用
        </button>
      </div>
      <p v-if="allAvailableTasks.length > 0" class="update-hint">
        建议先完成所有更新再启动应用。
      </p>
    </footer>
  </div>
</template>


<style scoped>
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

.component-list li {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 0;
  border-bottom: 1px solid #f0f0f0;
}

.component-list li:last-child {
  border-bottom: none;
}

.info {
  display: flex;
  flex-direction: column;
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

.local-version {
  text-decoration: line-through;
}

.arrow {
  margin: 0 0.5em;
  font-weight: bold;
}

.remote-version {
  color: #28a745;
  font-weight: bold;
}

.status-action {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.status-tag {
  font-size: 0.8em;
  padding: 0.2em 0.6em;
  border-radius: 12px;
  font-weight: bold;
  color: #fff;
}

.status-uptodate {
  background-color: #28a745;
}

.status-update_available {
  background-color: #ffc107;
  color: #333;
}

.status-not_installed {
  background-color: #6c757d;
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