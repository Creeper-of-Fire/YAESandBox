import type { App } from 'vue';
import type { PluginModule } from '@yaesandbox-frontend/core-services';
import { routes } from './routes'; // 导入你已有的 routes.ts

const EraLitePluginModule: PluginModule = {
    // Vue 插件对象
    plugin: {
        install: (app: App) => {
            console.log('era-lite插件安装完毕。');
            // 可以在这里注册全局组件等
        }
    },
    // 插件的路由
    routes,
    // 插件的元数据
    meta: {
        name: 'era-lite',
        uniqueName:'era-lite-A3F83C56-1D0E-4E10-97C4-C069C968AC39',
        navEntry: {
            label: 'era测试',
            order: 1
        }
    }
};

export default EraLitePluginModule;