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
        await updateConfigValue('Manifests', 'core_components_manifest_url', newUrl);
    }

    /**
     * 一个健壮的函数，用于更新或创建配置项。
     * 它通过字符串操作来保留原始文件的注释和格式。
     * @param section - 配置项所在的节，例如 "Network"。
     * @param key - 要更新的键，例如 "proxy_address"。
     * @param value - 要设置的新值。
     */
    async function updateConfigValue(section: string, key: string, value: string) {
        if (!rawContent.value) {
            console.error("无法修改配置，因为原始配置为空。");
            return;
        }

        let newContent = rawContent.value;
        const keyRegex = new RegExp(`^(\\s*${key}\\s*=\\s*).*$`, 'm');

        // 案例 1: key 已存在，直接替换该行的值。
        if (keyRegex.test(newContent)) {
            newContent = newContent.replace(keyRegex, `$1"${value}"`);
        } else {
            const sectionRegex = new RegExp(`^\\s*\\[${section}\\]`, 'm');
            // 案例 2: key 不存在，但 section 存在。在 section 头部追加新 key。
            if (sectionRegex.test(newContent)) {
                // $& 代表整个匹配到的字符串，即 `[SectionName]`
                newContent = newContent.replace(sectionRegex, `$&\n${key} = "${value}"`);
            }
            // 案例 3: key 和 section 都不存在。在文件末尾追加新的 section 和 key。
            else {
                // trim() 确保在追加前移除末尾可能存在的空行
                newContent = `${newContent.trim()}\n\n[${section}]\n${key} = "${value}"`;
            }
        }

        // 只有在内容实际发生变化时才执行保存，避免不必要的文件写入
        if (newContent !== rawContent.value) {
            await saveConfig(newContent);
        }
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