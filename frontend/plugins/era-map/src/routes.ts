import type {RouteRecordRaw} from 'vue-router';

export const routeName: string = 'era-map';

export const routes: RouteRecordRaw[] = [
    {
        path: `/${routeName}`,
        component: () => import('#/saves/ui/PluginRoot.vue'),
        children: [
            {
                path: '',
                component: () => import('#/EraMapView.vue'),
            }
        ]
    }
];