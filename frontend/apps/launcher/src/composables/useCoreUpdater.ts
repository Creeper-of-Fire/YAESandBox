// src/composables/useCoreUpdater.ts

import {readonly, ref} from 'vue';
import {invoke} from '@tauri-apps/api/core';
import semver from 'semver';
import type {UpdateTask} from './useTaskManager'; // 从我们的任务管理器导入类型
import {useConfig} from './useConfig';

// --- 类型定义 ---

// 远程核心组件清单中的条目结构
export interface RemoteCoreComponent {
    id: string;
    name: string;
    version: string;
    notes?: string;
    url: string;
    hash: string;
}

// 远程核心清单的完整结构
export interface CoreManifest {
    components: RemoteCoreComponent[];
}

/**
 * 核心组件的配置，由 App.vue 提供。
 * 包含每个组件的 ID 和其对应的解压路径。
 */
export interface ComponentConfig {
    id: string;
    extractPath: string;
}

// --- Composable 实现 ---

export function useCoreUpdater() {
    const {config} = useConfig();

    // 存储检查后发现的可用更新任务
    const availableUpdateTasks = ref<UpdateTask[]>([]);

    // 存储从清单读取的所有核心组件
    const allRemoteComponents = ref<RemoteCoreComponent[]>([]);

    // 跟踪检查过程的状态
    const isChecking = ref(false);
    const error = ref<string | null>(null);

    /**
     * 检查核心组件的更新。
     * @param componentConfigs - 一个包含核心组件 ID 和解压路径配置的数组。这是此 Composable 的外部依赖，由调用方（App.vue）提供。
     * @param remoteManifest - (可选) 一个预先获取的远程清单。如果提供，将跳过网络请求。
     */
    const checkCoreUpdates = async (componentConfigs: ComponentConfig[],
                                    remoteManifest?: CoreManifest | null) => {
        if (isChecking.value) return;

        isChecking.value = true;
        error.value = null;
        availableUpdateTasks.value = [];

        console.log('[CoreUpdater] 开始检查核心组件更新...');

        // --- 日志埋点 1: 检查配置 ---
        if (!config.value || !config.value.core_components_manifest_url) {
            const errorMsg = '[CoreUpdater] 错误: 配置未加载或清单 URL 为空!';
            console.error(errorMsg, {config: config.value});
            error.value = errorMsg;
            isChecking.value = false;
            return;
        }

        const configMap = new Map(componentConfigs.map(c => [c.id, c]));

        try {
            let manifestToUse: CoreManifest;

            // 如果外部传入了清单，则直接使用；否则，自己获取。
            if (remoteManifest) {
                console.log('[CoreUpdater] 使用了预先获取的远程清单。');
                manifestToUse = remoteManifest;
            } else {
                console.log('[CoreUpdater] 没有预获取的清单，正在从网络获取...');
                const manifestUrl = config.value.core_components_manifest_url;
                const proxyAddress = config.value.proxy_address;
                console.log('[CoreUpdater] 准备调用后端命令，参数如下:', {
                    manifestUrl,
                    proxyAddress,
                });
                manifestToUse = await invoke<CoreManifest>('fetch_manifest', {
                    url: manifestUrl,
                    proxy: proxyAddress,
                });
            }
            const [localVersions] = await Promise.all([
                invoke<Record<string, string | null>>('get_local_versions'),
            ]);
            // --- 打印从后端获取到的原始数据 ---
            console.log('[CoreUpdater] 成功从后端获取数据:');
            console.log('  - Local Versions:', JSON.parse(JSON.stringify(localVersions)));
            console.log('  - Remote Manifest:', JSON.parse(JSON.stringify(remoteManifest)));

            const remoteComponents = manifestToUse.components || []; // 增加保护，防止 components 不存在
            allRemoteComponents.value = remoteComponents;

            // --- 确认要比较的组件数量 ---
            console.log(`[CoreUpdater] 远程清单包含 ${remoteComponents.length} 个组件。开始比较...`);

            const tasks: UpdateTask[] = [];
            for (const remoteComponent of remoteComponents) {
                if (remoteComponent.id === 'launcher') {
                    continue;
                }

                const localVersion = localVersions[remoteComponent.id];
                const componentConfig = configMap.get(remoteComponent.id);

                // --- 打印每一次比较的详细信息 ---
                console.log(`[CoreUpdater] 正在比较组件: ${remoteComponent.name} (${remoteComponent.id})`);
                console.log(`  - 远程版本: ${remoteComponent.version}`);
                console.log(`  - 本地版本: ${localVersion}`);

                if (!componentConfig) {
                    console.warn(`  - 警告: 组件 "${remoteComponent.id}" 缺少本地配置，已跳过。`);
                    continue;
                }

                if (!localVersion || semver.gt(remoteComponent.version, localVersion)) {
                    console.log(`  - 结果: 需要更新！(原因: ${!localVersion ? '本地未安装' : '远程版本更高'})`);
                    tasks.push({
                        id: remoteComponent.id,
                        name: remoteComponent.name,
                        version: remoteComponent.version,
                        url: remoteComponent.url,
                        hash: remoteComponent.hash,
                        extractPath: componentConfig.extractPath, // 从配置中获取解压路径
                    });
                } else {
                    console.log('  - 结果: 无需更新。');
                }
            }

            availableUpdateTasks.value = tasks;
            console.log(`[CoreUpdater] 检查完成，发现 ${tasks.length} 个可用更新。`);

        } catch (e) {
            const errorMessage = `检查核心组件更新失败: ${String(e)}`;
            error.value = errorMessage;
            console.error(errorMessage, e);
        } finally {
            isChecking.value = false;
        }
    };

    return {
        // 只读的 ref，供 UI 使用
        coreUpdateTasks: readonly(availableUpdateTasks),
        allCoreComponents: readonly(allRemoteComponents),
        isCheckingCore: readonly(isChecking),
        coreCheckError: readonly(error),

        // 暴露给外部的方法
        checkCoreUpdates,
    };
}