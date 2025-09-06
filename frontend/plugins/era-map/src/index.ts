import {type App} from 'vue';
import type {PluginModule} from '@yaesandbox-frontend/core-services';
import {routeName, routes} from './routes';
import VueKonva from 'vue-konva';

const EraMapPluginModule: PluginModule = {
    // Vue 插件对象
    plugin: {
        install: (app: App) =>
        {
            console.log(`${routeName}插件安装完毕。`);
            // 可以在这里注册全局组件等
            app.use(VueKonva);
        }
    },
    // 插件的路由
    routes,
    // 插件的元数据
    meta: {
        name: routeName,
        uniqueName: `${routeName}-D9136330-BD4C-4496-BAC6-875BB920AA7C`,
        navEntry: {
            label: 'era地图',
            order: 1
        }
    }
};

export default EraMapPluginModule;