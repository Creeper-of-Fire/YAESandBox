// src/composables/useConfig.ts

import {readonly, ref} from 'vue';
import {invoke} from '@tauri-apps/api/core';

// 定义 AppConfig 接口，作为单一事实来源
export interface AppConfig {
    plugins_manifest_url: string;
    core_components_manifest_url: string;
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
        let value = trimmedLine.substring(firstEqualIndex + 1).trim();
        // 检查并移除包裹值的双引号或单引号
        if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
            value = value.substring(1, value.length - 1);
        }
        result[key] = value;
    }
    return result;
}

// 将状态定义在 composable 函数外部，使其成为单例
// 这样整个应用只会请求一次配置
const config = ref<AppConfig | null>(null);
const isLoading = ref(true);
const error = ref<string | null>(null);

const loadConfig = async (): Promise<boolean> => {
    if (config.value !== null) {
        // 如果已经加载过，直接返回成功，不再重复加载
        console.log('[useConfig] 配置已加载，跳过重复请求。');
        isLoading.value = false;
        return true;
    }

    console.log('[useConfig] 开始加载配置...');
    isLoading.value = true;
    error.value = null;

    try {
        // --- 日志埋点 1 ---
        console.log('[useConfig] 正在调用 Rust 命令: read_config_as_string');
        const configText = await invoke<string>('read_config_as_string');

        // --- 日志埋点 2 ---
        console.log('[useConfig] Rust 命令成功返回。接收到的原始文本:', { configText });

        // 增加对空字符串的健壮性处理
        if (typeof configText !== 'string' || configText.trim() === '') {
            console.error('[useConfig] 错误：从后端接收到的配置文本为空或无效。');
            error.value = '从后端接收到的配置文本为空或无效。';
            return false;
        }

        const parsed = parseIni(configText);
        // --- 日志埋点 3 ---
        console.log('[useConfig] INI 文本解析完成。解析结果:', parsed);

        config.value = {
            plugins_manifest_url: parsed.plugins_manifest_url || '',
            core_components_manifest_url: parsed.core_components_manifest_url || '',
            proxy_address: parsed.proxy_address || null,
        };

        // --- 日志埋点 4 ---
        console.log('[useConfig] 配置状态对象已成功赋值:', config.value);

        return true;
    } catch (e) {
        // --- 日志埋点 5 (关键) ---
        // 如果代码进入这里，说明 invoke 的 promise 被 reject 了
        const errorMessage = `加载配置失败 (在 catch 块中捕获): ${String(e)}`;
        error.value = errorMessage;
        console.error(errorMessage, e); // 打印详细的错误对象
        return false;
    } finally {
        isLoading.value = false;
        console.log('[useConfig] loadConfig 函数执行完毕。');
    }
};

export function useConfig() {
    // 返回状态，并暴露 reload 方法
    return {
        config: readonly(config),
        isLoading: readonly(isLoading),
        error: readonly(error),
        reloadConfig: loadConfig,
    };
}