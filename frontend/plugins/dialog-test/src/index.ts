import type { App } from 'vue';
import type { PluginModule } from '@yaesandbox-frontend/core-services';
import { routes } from './routes'; // 导入你已有的 routes.ts

const DialogTestPluginModule: PluginModule = {
    // Vue 插件对象
    plugin: {
        install: (app: App) => {
            console.log('工作流测试插件安装完毕。');
            // 可以在这里注册全局组件等
        }
    },
    // 插件的路由
    routes,
    // 插件的元数据
    meta: {
        name: 'dialog-test',
        uniqueName: "dialog_test_03E211F8-54B7-4661-BAF3-145E2570885C",
        navEntry: {
            label: '对话测试',
            order: 1
        }
    }
};

export default DialogTestPluginModule;