// src/composables/usePlugins.ts

import {readonly, ref} from 'vue';
import {invoke} from '@tauri-apps/api/core';
import {useConfig} from './useConfig'; // <-- 它将依赖 useConfig

export interface PluginInfo {
    id: string;
    name: string;
    version: string;
    description: string;
    url: string;
}

const plugins = ref<PluginInfo[]>([]);
const isLoading = ref(false);
const error = ref<string | null>(null);

export function usePlugins() {
    // 获取配置
    const { config } = useConfig();

    // 提供一个显式的 fetch 方法
    const fetchPlugins = async () => {
        // 确保配置已加载，并且没有正在进行的请求
        if (!config.value || isLoading.value) return;

        isLoading.value = true;
        error.value = null;
        try {
            // 从配置中获取 URL，然后作为参数传递给 Rust
            const manifestUrl = config.value?.plugins_manifest_url;
            plugins.value = await invoke<PluginInfo[]>('fetch_plugins_manifest', {
                url: manifestUrl,
                proxy: config.value?.proxy_address,
            });
        } catch (e) {
            const errorMessage = `加载插件失败: ${String(e)}`;
            error.value = errorMessage;
            console.error(errorMessage);
        } finally {
            isLoading.value = false;
        }
    };

    // 返回状态和方法
    return {
        plugins: readonly(plugins),
        isLoading: readonly(isLoading),
        error: readonly(error),
        fetchPlugins,
    };
}