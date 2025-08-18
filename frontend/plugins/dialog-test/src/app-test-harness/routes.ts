import type { RouteRecordRaw } from 'vue-router';
import TestHarnessView from './TestHarnessView.vue';

export const routes: RouteRecordRaw[] = [
    {
        path: '/dialog-test',
        name: 'TestHarness',
        component: TestHarnessView,
        meta: {
            title: '工作流测试台',
            requiresAuth: true // 显式声明
        }
    },
];