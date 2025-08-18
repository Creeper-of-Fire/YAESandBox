import type { RouteRecordRaw } from 'vue-router';

export const routes: RouteRecordRaw[] = [
    {
        path: '/dialog-test',
        name: 'DialogView',
        // 使用动态导入进行懒加载
        component: () => import('./DialogView.vue'),
        meta: {
            title: '对话测试',
            requiresAuth: true
        }
    },
];