// src/router/routerIndex.ts

import {createRouter, createWebHistory, type RouteRecordRaw} from 'vue-router';

// 1. 导入共享的/核心的视图组件 (如果直接在主路由中定义)
// 假设这是游戏模式的入口视图
// import NotFoundView from '@/views/NotFoundView.vue'; // 404页面示例
// 2. 导入特性符文暴露的路由配置
//    每个特性符文内部可以有一个 routes.ts 文件，导出该特性相关的子路由数组
import {routes as authRoutes} from '@/app-authentication/routes.ts';
import {useAuthStore} from "@/app-authentication/stores/authStore.ts";

// 3. 定义主路由规则 (顶层路由)
const shellRoutes: RouteRecordRaw[] = [
    {
        path: '/',
        name: 'Home', // 或者一个专门的欢迎/仪表盘页面
        // component: HomeView, // 如果有 HomeView
        redirect: '/workbench',
    },
    ...authRoutes,

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
    routes: shellRoutes, // 使用我们定义的主路由规则
    scrollBehavior(to, from, savedPosition)
    {
        // 控制滚动行为，例如切换路由时滚动到页面顶部
        if (savedPosition)
        {
            return savedPosition;
        }
        else
        {
            return {top: 0, behavior: 'smooth'};
        }
    }
});

// 5. 添加全局导航守卫
router.beforeEach((to, from, next) =>
{
    const authStore = useAuthStore();

    // 检查路由是否需要认证 (我们约定，没有 meta.requiresAuth 的都默认需要)
    const requiresAuth = to.meta.requiresAuth !== false;

    if (requiresAuth && !authStore.isAuthenticated)
    {
        // 如果需要登录但未登录，则重定向到登录页
        next({name: 'Login'});
    }
    else if (to.name === 'Login' && authStore.isAuthenticated)
    {
        // 如果已登录，访问登录页则自动跳转到主页
        next({name: 'Home'});
    }
    else
    {
        // 否则继续导航
        next();
    }
});


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