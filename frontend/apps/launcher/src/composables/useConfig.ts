// src/composables/useConfig.ts

import {readonly, ref} from 'vue';
import {invoke} from '@tauri-apps/api/core';

// 定义 AppConfig 接口，作为单一事实来源
export interface AppConfig {
    app_download_url: string;
    backend_download_url: string;
    plugins_manifest_url: string;
    proxy_address: string | null;
}

function parseIni(text: string): Record<string, string> {
    const result: Record<string, string> = {};
    const lines = text.split(/\r?\n/);
    for (const line of lines) {
        const trimmedLine = line.trim();
        if (trimmedLine.startsWith(';') || trimmedLine.startsWith('#') || !trimmedLine.includes('=')) {
            continue;
        }
        const firstEqualIndex = trimmedLine.indexOf('=');
        const key = trimmedLine.substring(0, firstEqualIndex).trim();
        result[key] = trimmedLine.substring(firstEqualIndex + 1).trim();
    }
    return result;
}

// 将状态定义在 composable 函数外部，使其成为单例
// 这样整个应用只会请求一次配置
const config = ref<AppConfig | null>(null);
const isLoading = ref(true);
const error = ref<string | null>(null);

const loadConfig = async () => {
    isLoading.value = true;
    error.value = null;
    try {
        const configText = await invoke<string>('read_config_as_string');
        const parsed = parseIni(configText);
        config.value = {
            app_download_url: parsed.app_url || '',
            backend_download_url: parsed.backend_url || '',
            plugins_manifest_url: parsed.plugins_manifest_url || '',
            proxy_address: parsed.proxy || null,
        };
    } catch (e) {
        const errorMessage = `加载配置失败: ${String(e)}`;
        error.value = errorMessage;
        console.error(errorMessage);
    } finally {
        isLoading.value = false;
    }
};

export function useConfig() {
    // 首次使用时，自动加载一次
    if (config.value === null && isLoading.value) {
        loadConfig().then(_ => {
        });
    }

    // 返回状态，并暴露 reload 方法
    return {
        config: readonly(config),
        isLoading: readonly(isLoading),
        error: readonly(error),
        reloadConfig: loadConfig,
    };
}