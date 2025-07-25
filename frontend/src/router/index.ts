﻿// src/router/index.ts

import {createRouter, createWebHistory, type RouteRecordRaw} from 'vue-router';

// 1. 导入共享的/核心的视图组件 (如果直接在主路由中定义)
// 假设这是游戏模式的入口视图
// import NotFoundView from '@/views/NotFoundView.vue'; // 404页面示例

// 2. 导入特性模块暴露的路由配置
//    每个特性模块内部可以有一个 routes.ts 文件，导出该特性相关的子路由数组
// import { routes as workflowEditorRoutes } from '@/app-view/';
import {routes as gamePlayerRoutes} from '@/app-game/routes.ts';
import { routes as workbenchRoutes } from '@/app-workbench/routes.ts';
import { routes as testHarnessRoutes } from '@/app-test-harness/routes.ts';
// import { routes as userProfileRoutes } from '@/features/user-profile/routes'; // 更多特性...

// 3. 定义主路由规则 (顶层路由)
const mainRoutes: RouteRecordRaw[] = [
    {
        path: '/',
        name: 'Home', // 或者一个专门的欢迎/仪表盘页面
        // component: HomeView, // 如果有 HomeView
        redirect: '/workbench',
    },
    ...gamePlayerRoutes,
    ...workbenchRoutes,
    ...testHarnessRoutes,
    // 可以将特性模块的路由作为顶层路由，如果它们有自己的完整页面布局
    // 这种方式下，特性模块的 routes.ts 导出的就是顶层路由配置了
    // ...workflowEditorRoutes, // 假设 workflowEditorRoutes 是 [{ path: '/editor/...', ... }]
    // ...userProfileRoutes,

    // 404 页面 (通常放在最后)
    // {
    //   path: '/:pathMatch(.*)*', // 匹配所有未匹配到的路径
    //   name: 'NotFound',
    //   component: NotFoundView,
    // },
];

// 4. 创建路由实例
const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL), // HTML5 History 模式
    routes: mainRoutes, // 使用我们定义的主路由规则
    scrollBehavior(to, from, savedPosition)
    {
        // 控制滚动行为，例如切换路由时滚动到页面顶部
        if (savedPosition)
        {
            return savedPosition;
        } else
        {
            return {top: 0, behavior: 'smooth'};
        }
    }
});

// 5. (可选) 添加全局导航守卫 (Navigation Guards)
// router.beforeEach((to, from, next) => {
//   if (to.meta.requiresAuth && !authStore.isAuthenticated) {
//     next({ name: 'Login' }); // 如果需要登录但未登录，则重定向到登录页
//   } else {
//     next(); // 否则继续导航
//   }
// });

// router.afterEach((to, from) => {
//   // 可以在这里更新页面标题等
//   if (to.meta.title) {
//     document.title = `${to.meta.title} - YourAppName`;
//   } else {
//     document.title = 'YourAppName';
//   }
// });


// 6. 导出路由实例
export default router;