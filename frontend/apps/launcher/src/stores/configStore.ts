import {defineStore} from 'pinia';
import {ref, computed} from 'vue';
import {invoke} from '@tauri-apps/api/core';

// --- 常量定义 ---
const MANIFEST_URLS = {
    full: "https://github.com/Creeper-of-Fire/YAESandBox/releases/latest/download/core_components_manifest.json",
    slim: "https://github.com/Creeper-of-Fire/YAESandBox/releases/latest/download/core_components_slim_manifest.json"
};

export type ManifestMode = 'full' | 'slim' | 'custom';

export interface ParsedConfig {
    core_components_manifest_url: string;
    plugins_manifest_url: string;
    proxy_address: string | null;
}

// --- 辅助函数 ---
function parseIni(text: string): Record<string, string> {
    const result: Record<string, string> = {};
    const lines = text.split(/\r?\n/);
    for (const line of lines) {
        const trimmedLine = line.trim();
        if (trimmedLine.startsWith(';') || trimmedLine.startsWith('#') || !trimmedLine.includes('=')) continue;
        const firstEqualIndex = trimmedLine.indexOf('=');
        const key = trimmedLine.substring(0, firstEqualIndex).trim();
        let value = trimmedLine.substring(firstEqualIndex + 1).trim();
        if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
            value = value.substring(1, value.length - 1);
        }
        result[key] = value;
    }
    return result;
}

export const useConfigStore = defineStore('config', () => {
    // --- State ---
    const rawContent = ref<string>('');
    const isLoading = ref(false);
    const error = ref<string | null>(null);

    // --- Getters ---
    const parsedConfig = computed<ParsedConfig | null>(() => {
        if (!rawContent.value) return null;
        const parsed = parseIni(rawContent.value);
        return {
            core_components_manifest_url: parsed.core_components_manifest_url || '',
            plugins_manifest_url: parsed.plugins_manifest_url || '',
            proxy_address: parsed.proxy_address || null,
        };
    });

    const currentMode = computed<ManifestMode>(() => {
        const url = parsedConfig.value?.core_components_manifest_url;
        if (url === MANIFEST_URLS.full) return 'full';
        if (url === MANIFEST_URLS.slim) return 'slim';
        return 'custom';
    });

    // --- Actions ---
    async function loadConfig() {
        isLoading.value = true;
        error.value = null;
        try {
            rawContent.value = await invoke('read_config_as_string');
        } catch (e) {
            error.value = `加载配置失败: ${String(e)}`;
            console.error(error.value);
        } finally {
            isLoading.value = false;
        }
    }

    async function changeManifestUrl(newUrl: string) {
        if (!rawContent.value) {
            console.error("无法修改配置，因为原始配置为空。");
            return;
        }

        // 使用正则表达式安全地替换，保留注释和格式
        const key = 'core_components_manifest_url';
        const regex = new RegExp(`^(\\s*${key}\\s*=\\s*).*$`, 'm');
        let newContent: string;

        if (regex.test(rawContent.value)) {
            newContent = rawContent.value.replace(regex, `$1"${newUrl}"`);
        } else {
            // 如果 key 不存在，则在 [Manifests] 节下追加
            const manifestSection = '[Manifests]';
            if (rawContent.value.includes(manifestSection)) {
                newContent = rawContent.value.replace(
                    manifestSection,
                    `${manifestSection}\n${key} = "${newUrl}"`
                );
            } else {
                // 如果连 [Manifests] 节都没有，则在文件末尾追加
                newContent = `${rawContent.value}\n\n[Manifests]\n${key} = "${newUrl}"`;
            }
        }

        await saveConfig(newContent);
    }

    async function saveConfig(content: string) {
        try {
            await invoke('write_config_as_string', { content });
            // 保存成功后，立即重新加载，确保状态与文件同步
            await loadConfig();
        } catch (e) {
            error.value = `保存配置失败: ${String(e)}`;
            console.error(error.value);
        }
    }

    return {
        // State
        rawContent,
        isLoading,
        error,
        // Getters
        parsedConfig,
        currentMode,
        // Actions
        loadConfig,
        changeManifestUrl,
        MANIFEST_URLS, // 暴露常量以便UI使用
    };
});