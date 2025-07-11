﻿import type { RouteRecordRaw } from 'vue-router';
import WorkbenchView from '@/app-workbench/WorkbenchView.vue';

export const routes: RouteRecordRaw[] = [
    {
        path: '/workbench',
        name: 'Workbench',
        component: WorkbenchView,
        meta: {
            title: '工作流编辑器'
        }
        // 如果工作台内部还有子路由（例如/workbench/settings），可以在这里定义children
        // children: []
    },
];