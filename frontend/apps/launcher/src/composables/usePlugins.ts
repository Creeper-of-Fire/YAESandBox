// src/composables/usePlugins.ts
import { ref, readonly } from 'vue';
import { invoke } from '@tauri-apps/api/core';
import semver from 'semver';
import type { UpdateTask } from './useTaskManager';
import { useConfig } from './useConfig';

// 远程插件清单中的条目结构
export interface RemotePluginInfo {
    id: string;
    name: string;
    version: string;
    description: string;
    url: string;
    hash: string;
}

export function usePlugins() {
    const { config } = useConfig();
    const availableUpdateTasks = ref<UpdateTask[]>([]);
    const allRemotePlugins = ref<RemotePluginInfo[]>([]);
    const isChecking = ref(false);
    const error = ref<string | null>(null);

    const checkPluginUpdates = async () => {
        if (isChecking.value) return;

        isChecking.value = true;
        error.value = null;
        availableUpdateTasks.value = [];

        try {
            // 1. 获取本地版本和远程清单
            const [localVersions, remoteManifest] = await Promise.all([
                invoke<Record<string, string>>('get_local_versions'),
                invoke<RemotePluginInfo[]>('fetch_manifest', {
                    url: config.value?.plugins_manifest_url,
                    proxy: config.value?.proxy_address,
                })
            ]);

            const remotePlugins = remoteManifest;
            allRemotePlugins.value = remotePlugins;

            // 2. 比较版本，生成更新任务
            const tasks: UpdateTask[] = [];
            for (const plugin of remotePlugins) {
                const localVersion = localVersions[plugin.id];
                if (!localVersion || semver.gt(plugin.version, localVersion)) {
                    tasks.push({
                        id: plugin.id,
                        name: plugin.name,
                        version: plugin.version,
                        url: plugin.url,
                        hash: plugin.hash,
                        extractPath: `Plugins/${plugin.id}` // 插件的解压路径是固定的
                    });
                }
            }
            availableUpdateTasks.value = tasks;

        } catch (e) {
            error.value = `检查插件更新失败: ${String(e)}`;
            console.error(error.value, e);
        } finally {
            isChecking.value = false;
        }
    };

    return {
        pluginUpdateTasks: readonly(availableUpdateTasks),
        allPlugins: readonly(allRemotePlugins),
        isCheckingPlugins: readonly(isChecking),
        pluginCheckError: readonly(error),
        checkPluginUpdates,
    };
}