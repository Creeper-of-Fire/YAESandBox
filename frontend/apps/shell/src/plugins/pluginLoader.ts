import {type App, type Component, h} from 'vue';
import router from '#/router/routerIndex'; // 导入主应用的 router 实例
import type {PluginModule} from '@yaesandbox-frontend/core-services';

// --- 开发时：静态导入所有插件 ---
// 这种方式能提供最好的开发体验
import workbenchPlugin from '@yaesandbox-frontend/plugin-workbench';
import dialogTestPlugin from '@yaesandbox-frontend/plugin-dialog-test';
import eraLitePlugin from '@yaesandbox-frontend/plugin-era-lite';
import PluginProvider from "#/component/PluginProvider.vue";
import {PluginUniqueNameKey} from "@yaesandbox-frontend/core-services/injectKeys";
import type {RouteComponent} from "vue-router";
import type {Pinia} from "pinia";
// import gamePlugin from '@yaesandbox-frontend/plugin-game';
// import testHarnessPlugin from '@yaesandbox-frontend/plugin-dialog-test-harness';
// import dialogPlugin from '@yaesandbox-frontend/plugin-dialog';

// 将所有静态导入的插件放入一个数组
const localPlugins: PluginModule[] = [
    workbenchPlugin,
    dialogTestPlugin,
    eraLitePlugin,
    // gamePlugin,
    // testHarnessPlugin,
    // dialogPlugin,
];

type Lazy<T> = () => Promise<T>;
type RawRouteComponent = RouteComponent | Lazy<RouteComponent>;

function wrapComponentWithProvider(component: RawRouteComponent, pluginUniqueName: string): RouteComponent
{
    // --- 关键：检测 component 是否为懒加载函数 ---
    if (typeof component === 'function') {
        // 如果是函数，我们返回一个新的懒加载函数
        return async () => {
            // 1. 执行原始的 import() 函数，等待组件模块加载完成
            const resolvedModule = await (component as () => Promise<any>)();
            // 2. 从模块中提取出真正的组件定义（处理 ESM 的 default 导出）
            const actualComponent = resolvedModule.default || resolvedModule;
            // 3. 返回一个包裹了真实组件的新组件定义
            return {
                render: () => h(PluginProvider, { pluginUniqueName }, {
                    default: () => h(actualComponent)
                })
            };
        };
    }

    // 如果 component 是一个普通的对象，我们同步地返回包装器
    return {
        render: () => h(PluginProvider, { pluginUniqueName }, {
            default: () => h(component as Component)
        })
    };
}

/**
 * 加载并安装所有已发现的插件
 * @param app Vue 应用实例
 * @param pinia Pinia
 */
export async function loadPlugins(app: App, pinia: Pinia)
{
    console.log('加载插件中...');

    app.provide(PluginUniqueNameKey, "app-shell-default-key-1DA56D62-6F82-401A-B5DC-D4E95F902B39");

    // 在生产环境中，你可以从 API 获取插件列表
    // const remotePlugins = await fetchPluginsFromApi();
    const allPlugins = [...localPlugins /*, ...remotePlugins*/];

    for (const pluginModule of allPlugins)
    {
        try
        {
            // 1. 安装 Vue 插件 (执行其 install 方法)
            app.use(pluginModule.plugin, pinia);

            // 从元数据中读取持久化的 uniqueName
            const pluginUniqueName = pluginModule.meta.uniqueName;

            // 2. 动态注册插件的路由
            pluginModule.routes.forEach(route =>
            {
                // 在这里可以给路由自动添加 meta 信息，比如插件名
                route.meta = {...route.meta, plugin: pluginModule.meta.name};
                if (route.component)
                {
                    route.component = wrapComponentWithProvider(route.component, pluginUniqueName);
                }
                router.addRoute(route);
            });

            console.log(`插件 "${pluginModule.meta.name}" 加载成功。`);
        } catch (e)
        {
            console.error(`插件 "${pluginModule.meta.name}" 加载失败：`, e);
        }
    }

    // 返回加载的插件元数据，供主应用使用（例如生成导航栏）
    return allPlugins.map(p => p.meta);
}

// 可选：生产环境的动态加载逻辑
async function fetchPluginsFromApi(): Promise<PluginModule[]>
{
    // const response = await fetch('/api/plugins/manifest');
    // const manifest = await response.json(); // [{ name: '...', entry: '...' }]
    // const loadedModules = await Promise.all(
    //     manifest.map(item => import(/* @vite-ignore */ item.entry))
    // );
    // return loadedModules.map(mod => mod.default);
    return []; // 暂时返回空
}