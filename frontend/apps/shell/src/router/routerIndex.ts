// src/router/routerIndex.ts

import {createRouter, createWebHistory, type RouteRecordRaw} from 'vue-router';

// 1. 导入共享的/核心的视图组件 (如果直接在主路由中定义)
// 假设这是游戏模式的入口视图
// import NotFoundView from '#/views/NotFoundView.vue'; // 404页面示例
// 2. 导入特性符文暴露的路由配置
//    每个特性符文内部可以有一个 routes.ts 文件，导出该特性相关的子路由数组
import {routes as authRoutes} from '#/app-authentication/routes.ts';
import {useAuthStore} from "#/app-authentication/stores/authStore.ts";
import HomeView from "#/view/HomeView.vue";
import AboutView from "#/view/AboutView.vue";


// 3. 定义主路由规则 (顶层路由)
const shellRoutes: RouteRecordRaw[] = [
    {
        path: '/',
        name: 'App_Shell_HomeView',
        component: HomeView,
        meta: {requiresAuth: false}
    },
    {
        path: '/about',
        component: AboutView,
        meta: {requiresAuth: false}
    },
    ...authRoutes,

    // 404 页面 (通常放在最后)
    // {
    //   path: '/:pathMatch(.*)*', // 匹配所有未匹配到的路径
    //   name: 'NotFound',
    //   component: NotFoundView,
    // },
];

/**
 * 检查路由名称的唯一性，并在发现重复时抛出致命错误。
 * @param routes 要检查的路由记录数组
 */
function assertUniqueRouteNames(routes: RouteRecordRaw[]): void
{
    const seenNames = new Map<string | symbol, string>(); // 使用 Map 来存储见过的名称及其来源路径

    function check(route: RouteRecordRaw)
    {
        if (route.name)
        {
            const nameKey = route.name;
            if (seenNames.has(nameKey))
            {
                // 发现重复！
                const originalPath = seenNames.get(nameKey);
                const errorMessage = `
============================== 致命启动错误 ==============================
          检测到重复的路由名称: "${String(route.name)}"
--------------------------------------------------------------------------------
此路由名称先前已为路径注册: "${originalPath}"
现在它又被非法地为路径重新注册: "${route.path}"

为何这是致命错误:
路由名称在整个应用（包括所有插件）中【必须】是唯一的。
重复的名称会导致不可预测的导航和静默失败。

如何修复:
1. 找到路径为 "${route.path}" 的路由定义文件。
2.a 将其 "name" 属性更改为唯一的值，通常是添加一个特定于插件的前缀
   (例如: "MyPlugin_${String(route.name)}")。
2.b 在路由的 "name" 属性后面添加专门的、硬编码uuid。
2.c 不使用带有 "name" 属性的“具名路由”。

应用程序启动已被中止。
================================================================================
`;

                // 直接抛出错误，让应用崩溃！
                throw new Error(errorMessage);
            }
            seenNames.set(nameKey, route.path);
        }

        // 递归检查子路由
        if (route.children)
        {
            route.children.forEach(check);
        }
    }

    routes.forEach(check);
}

export function createRouterInstance(dynamicRoutes: RouteRecordRaw[])
{
    const allRoutes = [...shellRoutes, ...dynamicRoutes];
    assertUniqueRouteNames(allRoutes)
    // 创建路由实例
    const router = createRouter({
        history: createWebHistory(import.meta.env.BASE_URL), // HTML5 History 模式
        routes: allRoutes, // 使用我们定义的主路由规则
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

    // 添加全局导航守卫
    router.beforeEach((to, from, next) =>
    {
        const authStore = useAuthStore();

        // 检查路由是否需要认证 (我们约定，没有 meta.requiresAuth 的都默认需要)
        const requiresAuth = to.meta.requiresAuth ?? true;

        if (requiresAuth && !authStore.isAuthenticated)
        {
            // 如果需要登录但未登录，则重定向到登录页
            next({name: 'App_Shell_Login', query: {redirect: to.fullPath}});
        }
        else if (to.name === 'App_Shell_Login' && authStore.isAuthenticated)
        {
            // 如果已登录，访问登录页则自动跳转到主页
            next({name: 'App_Shell_HomeView'});
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

    return router;
}