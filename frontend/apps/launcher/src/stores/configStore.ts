import {defineStore} from 'pinia';
import {computed, type ComputedRef, ref} from 'vue';
import {invoke} from '@tauri-apps/api/core';

// 与 Rust 后端的 ConfigEntry 完全对应
export interface ConfigEntry
{
    section: string;
    key: string;
    value: string;
    comments: string[];
}

export type ManifestMode = 'full' | 'slim' | 'custom';

// --- 常量定义 ---
const MANIFEST_URLS = {
    full: "https://github.com/Creeper-of-Fire/YAESandBox/releases/latest/download/core_components_manifest.json",
    slim: "https://github.com/Creeper-of-Fire/YAESandBox/releases/latest/download/core_components_slim_manifest.json"
};

const CORE_MANIFEST_KEY = 'core_components_manifest_url';

export const useConfigStore = defineStore('config', () =>
{
    // --- State ---
    const configEntries = ref<ConfigEntry[]>([]);
    const isLoading = ref(false);
    const error = ref<string | null>(null);

    // --- Getters ---
    /**
     * 一个通用的 getter，用于通过 key 安全地获取配置值。
     * @param key - 要查找的配置键，例如 "proxy_address"
     */
    const getConfigValue = (key: string): ComputedRef<string | undefined> =>
    {
        return computed(() => configEntries.value.find(entry => entry.key === key)?.value);
    };

    /**
     * 根据核心组件清单URL，计算出当前的更新源模式。
     */
    const currentMode = computed<ManifestMode>(() =>
    {
        const url = getConfigValue(CORE_MANIFEST_KEY).value;
        if (url === MANIFEST_URLS.full) return 'full';
        if (url === MANIFEST_URLS.slim) return 'slim';
        return 'custom';
    });

    // --- Actions ---
    /**
     * 从后端加载所有配置项。
     */
    async function loadConfig()
    {
        isLoading.value = true;
        error.value = null;
        try
        {
            configEntries.value = await invoke('read_config');
        } catch (e)
        {
            error.value = `加载配置失败: ${String(e)}`;
            console.error(error.value);
            configEntries.value = []; // 出错时清空
        } finally
        {
            isLoading.value = false;
        }
    }

    /**
     * 立即更新单个配置项的值并同步到后端。
     * @param key - 要更新的配置键
     * @param newValue - 新的值
     */
    async function updateConfigValue(key: string, newValue: string) {
        const entryToUpdate = configEntries.value.find(e => e.key === key);
        if (!entryToUpdate) {
            console.error(`尝试更新一个不存在的配置项: ${key}`);
            return;
        }

        const oldValue = entryToUpdate.value;
        // 1. 乐观更新：立即修改前端状态，UI 响应迅速
        entryToUpdate.value = newValue;

        try {
            // 2. 将整个更新后的配置数组发送到后端
            await invoke('write_config', { entries: configEntries.value });
        } catch (e) {
            // 3. 回滚：如果后端保存失败，将前端状态恢复到之前的值
            entryToUpdate.value = oldValue;
            error.value = `保存配置 '${key}' 失败: ${String(e)}`;
            console.error(error.value);
            // 可以在此抛出错误或显示通知
            alert(`保存配置失败: ${String(e)}`);
        }
    }

    return {
        // State
        configEntries,
        isLoading,
        error,
        // Getters
        getConfigValue,
        currentMode,
        // Actions
        loadConfig,
        updateConfigValue,
        // Constants
        MANIFEST_URLS,
        CORE_MANIFEST_KEY
    };
});