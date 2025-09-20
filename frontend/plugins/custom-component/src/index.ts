import {type App} from 'vue';
import type {PluginModule} from '@yaesandbox-frontend/core-services';
import {routes,routeName} from './routes';

const CustomComponentPluginModule: PluginModule = {
    // Vue 插件对象
    plugin: {
        install: (app: App) =>
        {
            console.log('CustomComponent 插件安装完毕。');
            // 可以在这里注册全局组件等
        }
    },
    // 插件的路由
    routes,
    // 插件的元数据
    meta: {
        name: routeName,
        uniqueName: 'CustomComponent-421246D7-4EC8-49F8-8C16-692912D837E7',
        navEntry: {
            label: '自定义组件',
            order: 1
        }
    }
};

export default CustomComponentPluginModule;