<!-- src/components/SettingsPanel.vue -->
<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useConfigStore, type ManifestMode } from '../stores/configStore.ts';
import ConfirmationDialog from "./ConfirmationDialog.vue";
import AdvancedSettingsModal from "./AdvancedSettingsModal.vue";

const configStore = useConfigStore();

const showAdvancedSettingsModal = ref(false);

const themeValue = computed({
  get: () => configStore.getConfigValue('theme').value || 'auto',
  set: (newValue) => {
    configStore.updateConfigValue('theme', newValue);
  }
});

const coreManifestUrl = computed({
  get: () => configStore.getConfigValue(configStore.CORE_MANIFEST_KEY).value || '',
  set: (newValue) => {
    // 提示: 对于文本输入，可以加入 debounce (防抖) 来避免在每次按键时都调用后端
    configStore.updateConfigValue(configStore.CORE_MANIFEST_KEY, newValue);
  }
});

// --- 3. Manifest Mode Logic (with Slim Warning) ---

const showSlimWarningDialog = ref(false);
const slimWarningMessage = `您选择的“精简版”不包含 .NET 运行环境。

请确保您的系统已安装【.NET 9 (或更高版本) ASP.NET Core 运行时】，否则后端服务将无法启动。

您可以从微软官方网站下载：
<a href="https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0" target="_blank">https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0</a>`;


// 步骤 1: 创建一个本地的“代理” ref，专门给 v-model 使用。
const localSelectedMode = ref<ManifestMode>(configStore.currentMode);

// 步骤 2: (Store -> UI Sync) 监听 store 中的真实状态，同步到我们的本地代理 ref。
// 这确保了在配置加载或外部更改后，下拉菜单显示正确的值。
watch(() => configStore.currentMode, (newStoreMode) => {
  localSelectedMode.value = newStoreMode;
});

// 步骤 3: (UI -> Store Sync) 监听本地代理 ref 的变化（即用户的选择），并执行复杂逻辑。
watch(localSelectedMode, (newMode, oldMode) => {
  // 如果新旧模式相同，说明是 store -> UI 的同步触发的，无需处理。
  if (newMode === oldMode || newMode === configStore.currentMode) {
    return;
  }

  if (newMode === 'slim') {
    showSlimWarningDialog.value = true;
    // 关键：如果用户取消，我们需要将 UI 恢复到之前的状态。
    // 我们将这个“回滚”操作放在 handleSlimCancel 中。
  } else if (newMode === 'full') {
    configStore.updateConfigValue(configStore.CORE_MANIFEST_KEY, configStore.MANIFEST_URLS.full);
  } else if (newMode === 'custom') {
    // 切换到自定义模式时，我们什么都不做。
    // 这允许 localSelectedMode 的值保持为 'custom'，从而正确显示输入框。
    // 真正的 URL 更改将由 coreManifestUrl 的 v-model 触发。
  }
});

function handleSlimConfirm() {
  configStore.updateConfigValue(configStore.CORE_MANIFEST_KEY, configStore.MANIFEST_URLS.slim);
  showSlimWarningDialog.value = false;
  // 确认后，store.currentMode 会变为 'slim'，上面的第一个 watch 会自动更新 localSelectedMode。
}

function handleSlimCancel() {
  // 用户取消了操作，所以我们将本地代理 ref 的值恢复到 store 的当前状态。
  localSelectedMode.value = configStore.currentMode;
  showSlimWarningDialog.value = false;
}
</script>

<template>
  <div class="settings-panel">
    <!-- 更新源设置 -->
    <div class="setting-group">
      <label for="manifest-mode">更新源模式:</label>
      <select id="manifest-mode" v-model="localSelectedMode">
        <option value="full">完整版 (自带.NET环境)</option>
        <option value="slim">精简版 (需自行安装.NET)</option>
        <option value="custom">自定义</option>
      </select>
      <div v-if="localSelectedMode === 'custom'" class="custom-url-input">
        <input v-model="coreManifestUrl" placeholder="输入核心组件清单URL" type="text">
      </div>
    </div>

    <!-- 主题设置 -->
    <div class="setting-group">
      <label for="theme-mode">应用主题:</label>
      <!-- 将主题选择器和高级设置按钮放在一行 -->
      <div class="theme-actions">
        <select id="theme-mode" v-model="themeValue">
          <option value="auto">跟随系统</option>
          <option value="light">浅色模式</option>
          <option value="dark">深色模式</option>
        </select>
        <!-- 高级设置按钮 -->
        <button class="button-secondary" @click="showAdvancedSettingsModal = true">高级设置</button>
      </div>
    </div>

    <ConfirmationDialog
        v-if="showSlimWarningDialog"
        :message="slimWarningMessage"
        title="切换模式警告"
        @cancel="handleSlimCancel"
        @confirm="handleSlimConfirm"
    />

    <AdvancedSettingsModal v-model="showAdvancedSettingsModal"/>
  </div>
</template>

<style scoped>
.settings-panel {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  font-size: 0.9em;
  color: var(--text-color-secondary);
}

.setting-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.setting-group label {
  font-weight: 600;
  color: var(--text-color-primary);
}

select, input[type="text"] {
  width: 100%;
  box-sizing: border-box;
  padding: 0.5rem;
  border-radius: 4px;
  border: 1px solid var(--border-color-strong);
  background-color: var(--bg-color-panel);
  color: var(--text-color-primary);
}

.custom-url-input {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.theme-actions {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.theme-actions select {
  flex-grow: 1; /* 让选择框占据多余空间 */
}

.theme-actions button {
  flex-shrink: 0; /* 防止按钮被压缩 */
  padding: 0.5rem 0.8rem; /* 调整按钮大小以匹配输入框 */
}
</style>