import type { RouteRecordRaw } from 'vue-router';
import TestHarnessView from './TestHarnessView.vue';

export const routes: RouteRecordRaw[] = [
    {
        path: '/test',
        name: 'TestHarness',
        component: TestHarnessView,
        meta: {
            title: '工作流测试台'
        }
    },
];