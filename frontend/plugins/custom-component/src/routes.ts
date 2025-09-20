import type {RouteRecordRaw} from 'vue-router';

export const routeName = 'custom-component';
export const routes: RouteRecordRaw[] = [
    {
        // 插件的根路径，组件是我们的控制中心 PluginRoot.vue
        path: `/${routeName}`,
        component: () => import('#/saves/PluginRoot.vue')
    }
];