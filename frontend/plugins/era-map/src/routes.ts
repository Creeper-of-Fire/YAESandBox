import type {RouteRecordRaw} from 'vue-router';

export const routeName: string = 'era-map';

export const routes: RouteRecordRaw[] = [
    {
        path: `/${routeName}`,
        component: () => import('#/saves/PluginRoot.tsx'),
        children: [
            {
                path: '',
                component: () => import('#/views/CreatorView.vue'),
            }
        ]
    }
];