import type { App } from 'vue';
import router from '#/router/routerIndex'; // 导入主应用的 router 实例
import type { PluginModule } from '@yaesandbox-frontend/core-services';

// --- 开发时：静态导入所有插件 ---
// 这种方式能提供最好的开发体验
import workbenchPlugin from '@yaesandbox-frontend/plugin-workbench';
import dialogTestPlugin from '@yaesandbox-frontend/plugin-dialog-test';
// import gamePlugin from '@yaesandbox-frontend/plugin-game';
// import testHarnessPlugin from '@yaesandbox-frontend/plugin-dialog-test-harness';
// import dialogPlugin from '@yaesandbox-frontend/plugin-dialog';

// 将所有静态导入的插件放入一个数组
const localPlugins: PluginModule[] = [
    workbenchPlugin,
    dialogTestPlugin,
    // gamePlugin,
    // testHarnessPlugin,
    // dialogPlugin,
];

/**
 * 加载并安装所有已发现的插件
 * @param app Vue 应用实例
 */
export async function loadPlugins(app: App) {
    console.log('加载插件中...');

    // 在生产环境中，你可以从 API 获取插件列表
    // const remotePlugins = await fetchPluginsFromApi();
    const allPlugins = [...localPlugins /*, ...remotePlugins*/];

    for (const pluginModule of allPlugins) {
        try {
            // 1. 安装 Vue 插件 (执行其 install 方法)
            app.use(pluginModule.plugin);

            // 2. 动态注册插件的路由
            pluginModule.routes.forEach(route => {
                // 在这里可以给路由自动添加 meta 信息，比如插件名
                route.meta = { ...route.meta, plugin: pluginModule.meta.name };
                router.addRoute(route);
            });

            console.log(`插件 "${pluginModule.meta.name}" 加载成功。`);
        } catch (e) {
            console.error(`插件 "${pluginModule.meta.name}" 加载失败：`, e);
        }
    }

    // 返回加载的插件元数据，供主应用使用（例如生成导航栏）
    return allPlugins.map(p => p.meta);
}

// 可选：生产环境的动态加载逻辑
async function fetchPluginsFromApi(): Promise<PluginModule[]> {
    // const response = await fetch('/api/plugins/manifest');
    // const manifest = await response.json(); // [{ name: '...', entry: '...' }]
    // const loadedModules = await Promise.all(
    //     manifest.map(item => import(/* @vite-ignore */ item.entry))
    // );
    // return loadedModules.map(mod => mod.default);
    return []; // 暂时返回空
}