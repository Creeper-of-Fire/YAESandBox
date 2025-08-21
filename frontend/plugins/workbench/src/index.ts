import type {App} from 'vue';
import type {PluginModule} from '@yaesandbox-frontend/core-services';
import {routes} from './routes'; // 导入你已有的 routes.ts

const WorkbenchPluginModule: PluginModule = {
    // Vue 插件对象
    plugin: {
        install: (app: App) =>
        {
            console.log('Workbench plugin installed.');
            // 可以在这里注册工作台内部的全局组件等
        }
    },
    // 插件的路由
    routes,
    // 插件的元数据
    meta: {
        name: 'workbench',
        uniqueName: 'workbench-D1D3E394-8025-45A5-AD6E-9C1B8BCF3B38',
        navEntry: {
            label: '编辑器',
            order: 1
        }
    }
};

export default WorkbenchPluginModule;