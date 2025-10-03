import {defineStore} from 'pinia';
import {invoke} from '@tauri-apps/api/core';
import {listen, type UnlistenFn} from '@tauri-apps/api/event';
import semver from 'semver';
import {computed, type ComputedRef, type Reactive, reactive, ref, watch} from "vue";
import {useConfigStore} from "./configStore.ts";
import {launcherName} from "../utils/constant.ts";

// --- 1. 类型定义 ---

// 组件的各种状态，这是我们状态机的核心
export type ComponentStatus =
    | 'idle' // 初始状态
    | 'uptodate' // 最新
    | 'update_available' // 可更新
    | 'not_installed' // 未安装
    | 'downloading' // 下载中
    | 'pending_install' // 等待安装（已下载）
    | 'installing' // 安装中
    | 'error'; // 发生错误

// 统一的组件数据结构
export interface Component
{
    id: string;
    name: string;
    type: 'core' | 'plugin';
    localVersion: string | null;
    remoteVersion: string;
    notes?: string;
    description?: string;
    url: string;
    hash: string;
    extractPath: string;

    // 动态状态
    status: ComponentStatus;
    statusText: string;
    progress: { percentage: number; text: string };
    error: string | null;
}

// 用于Tauri事件的Payload类型
interface DownloadProgressPayload
{
    id: string;
    downloaded: number;
    total: number | null;
}

const CORE_COMPONENTS_CONFIG: { id: string; extractPath: string }[] = [
    {id: launcherName, extractPath: 'manual_downloads/launcher_update'},
    {id: 'app', extractPath: 'wwwroot'},
    {id: 'backend', extractPath: 'backend'},
];

// --- 2. Pinia Store 定义 ---
export const useUpdaterStore = defineStore('updater', () =>
{
    // --- 1. State (用 ref 定义) ---
    const components = ref<Record<string, Component>>({});
    const isChecking = ref(false);
    const isInstalling = ref(false);
    const globalStatusMessage = ref('启动器正在初始化...');
    const globalError = ref<string | null>(null);
    const configStore = useConfigStore();
    const config = computed(() => ({
        core_components_manifest_url: configStore.getConfigValue('core_components_manifest_url').value,
        plugins_manifest_url: configStore.getConfigValue('plugins_manifest_url').value,
        proxy_address: configStore.getConfigValue('proxy_address').value,
    }));

    let unlistenProgress: UnlistenFn | null = null;


    // --- 2. Getters (用 computed 定义) ---
    const launcherComponent = computed<Component | undefined>(() =>
    {
        return Object.values(components.value).find(c => c.id === 'launcher');
    });

    const coreComponents = computed(() => Object.values(components.value)
        .filter(c => c.type === 'core' && c.id !== launcherName)
        .sort((a, b) => a.name.localeCompare(b.name)));

    const pluginComponents = computed(() => Object.values(components.value)
        .filter(c => c.type === 'plugin')
        .sort((a, b) => a.name.localeCompare(b.name)));

    const availableUpdates = computed(() => Object.values(components.value)
        .filter(c => (c.status === 'not_installed' || c.status === 'update_available') && c.id !== launcherName));

    // const isBusy = computed(() => Object.values(components.value).some(c => ['downloading', 'installing', 'pending_install'].includes(c.status)));
    // isDownloading 只是一个状态查询，用于UI反馈，不用于逻辑锁定。
    const isDownloading = computed(() => Object.values(components.value).some(c => c.status === 'downloading'));


    // --- 3. Actions (业务逻辑) ---

    /**
     * 启动时执行一次，获取所有信息
     */
    async function initialize(): Promise<void>
    {
        isChecking.value = true;
        globalStatusMessage.value = '正在加载配置...';
        globalError.value = null;

        await configStore.loadConfig();

        if (!config.value.core_components_manifest_url || !config.value.plugins_manifest_url)
        {
            globalError.value = '配置文件无效或缺失关键URL。';
            globalStatusMessage.value = '配置错误。';
            isChecking.value = false;
            return;
        }

        try
        {
            globalStatusMessage.value = '正在获取版本信息...';

            const [localVersions, coreManifest, pluginManifest] = await Promise.all([
                invoke<Record<string, string>>('get_local_versions'),
                invoke<{ components: any[] }>('fetch_manifest', {
                    url: config.value.core_components_manifest_url,
                    proxy: config.value.proxy_address
                }),
                invoke<any[]>('fetch_manifest', {
                    url: config.value.plugins_manifest_url,
                    proxy: config.value.proxy_address
                })
            ]);

            const newComponents: Record<string, Component> = {};
            const configMap = new Map(CORE_COMPONENTS_CONFIG.map(c => [c.id, c]));

            for (const remote of coreManifest.components)
            {
                const cfg = configMap.get(remote.id);
                if (!cfg) continue;
                newComponents[remote.id] = processComponent(remote, localVersions, 'core', cfg.extractPath);
            }

            for (const remote of pluginManifest)
            {
                newComponents[remote.id] = processComponent(remote, localVersions, 'plugin', `Plugins/${remote.id}`);
            }

            components.value = newComponents;

            const updateCount = availableUpdates.value.length + (components.value[launcherName]?.status === 'update_available' ? 1 : 0);
            if (updateCount > 0)
            {
                globalStatusMessage.value = `发现 ${updateCount} 个可用更新。`;
            } else
            {
                globalStatusMessage.value = '所有组件都已是最新版本。';
            }

        } catch (e)
        {
            globalError.value = `初始化失败: ${String(e)}`;
            globalStatusMessage.value = '检查更新时发生错误。';
            console.error(e);
        } finally
        {
            isChecking.value = false;
        }
    }

    /**
     * 监听后端的下载进度事件
     */
    async function listenForProgress()
    {
        if (unlistenProgress)
            unlistenProgress();
        unlistenProgress = await listen<DownloadProgressPayload>('download-progress', (event) =>
        {
            const component = components.value[event.payload.id];
            if (component)
            {
                const {downloaded, total} = event.payload;
                if (total)
                {
                    component.progress.percentage = Math.round((downloaded / total) * 100);
                    component.progress.text = `${(downloaded / 1024 / 1024).toFixed(2)}MB / ${(total / 1024 / 1024).toFixed(2)}MB`;
                } else
                {
                    component.progress.text = `已下载 ${(downloaded / 1024 / 1024).toFixed(2)}MB`;
                }
            }
        });
    }

    /**
     * 下载单个组件（并行触发）
     */
    async function downloadComponent(id: string)
    {
        const component = components.value[id];
        if (!component || component.status === 'downloading' || isInstalling.value)
        {
            return;
        }

        component.status = 'downloading';
        component.statusText = '下载中';
        component.progress = {percentage: 0, text: '准备下载...'};
        component.error = null;

        const proxy = config.value.proxy_address;

        try
        {
            const savePath = `downloads/${component.id}.zip`;
            await invoke('download_and_verify_zip', {
                id, url: component.url, relativePath: savePath,
                expectedHash: component.hash, proxy: proxy,
            });
            component.status = 'pending_install';
            component.statusText = '等待安装';
            startInstallQueue();
        } catch (e)
        {
            component.status = 'error';
            component.statusText = '下载失败';
            component.error = String(e);
        }
    }

    /**
     * 查找下一个待安装的组件。这是一个内部辅助函数。
     * @returns {Component | undefined}
     */
    function findNextPendingInstall(): Component | undefined
    {
        // 这里可以加入优先级逻辑，比如先安装核心组件
        return Object.values(components.value).find(c => c.status === 'pending_install');
    }

    /**
     * 下载所有可用更新（启动器除外）
     */
    function downloadAll()
    {
        availableUpdates.value.forEach(component => downloadComponent(component.id));
    }

    /**
     * 这是一个持续运行的“安装工人”。
     * 只要 isInstalling 为 true，它就会不断检查队列并处理任务。
     * 它不应该被外部直接 await。
     */
    async function installationWorker()
    {
        // 只要这个“工人”还在上班 (isInstalling is true)
        while (isInstalling.value)
        {
            const componentToInstall = findNextPendingInstall();

            // 如果没有更多任务，工人下班
            if (!componentToInstall)
            {
                isInstalling.value = false;
                // console.log('[Installer] 队列已清空，工人下班。');
                return; // 退出循环
            }

            // console.log(`[Installer] 开始处理任务: ${componentToInstall.name}`);
            componentToInstall.status = 'installing';
            componentToInstall.statusText = '安装中...';

            try
            {
                const savePath = `downloads/${componentToInstall.id}.zip`;
                await invoke('unzip_file', {
                    zipRelativePath: savePath,
                    targetRelativeDir: componentToInstall.extractPath
                });
                await invoke('update_local_version', {
                    componentId: componentToInstall.id,
                    newVersion: componentToInstall.remoteVersion
                });
                await invoke('delete_file', {relativePath: savePath});

                componentToInstall.status = 'uptodate';
                componentToInstall.statusText = '最新';
                componentToInstall.localVersion = componentToInstall.remoteVersion;
                // console.log(`[Installer] 任务成功: ${componentToInstall.name}`);
            } catch (e)
            {
                componentToInstall.status = 'error';
                componentToInstall.statusText = '安装失败';
                componentToInstall.error = String(e);
                // console.error(`[Installer] 任务失败: ${componentToInstall.name}`, e);
            }
            // 当前任务处理完毕，循环会继续，自动寻找下一个任务
        }
    }

    /**
     * 启动安装流程。
     * 它的作用是叫醒“工人”，如果工人已经在工作，则什么也不做。
     * 这个函数是“Fire and Forget”，它的意图就是触发一个后台任务。
     */
    function startInstallQueue()
    {
        // 如果工人已经在工作了，就不用再叫他了
        if (isInstalling.value)
        {
            // console.log('[Installer] 工人已在工作中，无需再次启动。');
            return;
        }

        // 检查是否有需要处理的任务
        if (findNextPendingInstall())
        {
            // console.log('[Installer] 发现待处理任务，叫工人来上班。');
            isInstalling.value = true;
            // 叫醒工人，让他开始工作。我们不等待他完成所有工作。
            installationWorker().then(_ =>
            {
            });
        }
    }

    /**
     * 【特殊】处理启动器自更新
     */
    async function updateLauncher()
    {
        const component = components.value[launcherName];
        if (!component || isInstalling.value) return;

        component.status = 'downloading';
        component.statusText = '下载中';
        component.error = null;

        try
        {
            const savePath = `downloads/${component.id}_update.zip`;
            await invoke('download_and_verify_zip', {
                id: component.id, url: component.url, relativePath: savePath,
                expectedHash: component.hash, proxy: config.value.proxy_address,
            });

            globalStatusMessage.value = '正在应用更新，应用即将重启...';
            await invoke('apply_launcher_self_update', {
                zipRelativePath: savePath,
                newVersion: component.remoteVersion,
            });
        } catch (e)
        {
            component.status = 'error';
            component.statusText = '更新失败';
            component.error = String(e);
            globalStatusMessage.value = `启动器更新失败: ${String(e)}`;
        }
    }

    // --- 跨 Store 监听逻辑 ---

    watch(
        // 监听的源：核心组件清单的 URL。我们使用 .value 来访问 ComputedRef 的值。
        () => config.value.core_components_manifest_url,

        // 当 URL 变化时执行的回调
        (newUrl, oldUrl) => {
            // 这个判断非常重要：
            // 1. 确保新旧值都存在 (避免在初始加载时触发)
            // 2. 确保新旧值确实不同
            if (newUrl && oldUrl && newUrl !== oldUrl) {
                console.info(`[UpdaterStore] 检测到更新源变化，将自动刷新组件列表...`);
                // 直接调用本 store 的 initialize action
                initialize();
            }
        }
    );

    // --- 4. 返回需要暴露给外部的状态和方法 ---
    return {
        // State
        components,
        isChecking,
        isInstalling,
        globalStatusMessage,
        globalError,

        // Getters
        launcherComponent,
        coreComponents,
        pluginComponents,
        availableUpdates,
        isDownloading,

        // Actions
        initialize,
        listenForProgress,
        downloadComponent,
        downloadAll,
        updateLauncher,
    };
});

// --- 4. 辅助函数 ---
function processComponent(
    remote: any,
    localVersions: Record<string, string>,
    type: 'core' | 'plugin',
    extractPath: string
): Component
{
    const localVersion = localVersions[remote.id] || null;
    let status: ComponentStatus = 'not_installed';
    let statusText: string = '未安装';

    if (localVersion)
    {
        if (semver.gt(remote.version, localVersion))
        {
            status = 'update_available';
            statusText = '可更新';
        } else
        {
            status = 'uptodate';
            statusText = '最新';
        }
    }

    return {
        ...remote,
        type,
        extractPath,
        localVersion,
        remoteVersion: remote.version,
        status,
        statusText,
        progress: {percentage: 0, text: ''},
        error: null,
    };
}