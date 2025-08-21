import type { RouteRecordRaw } from 'vue-router';

export const routes: RouteRecordRaw[] = [
    {
        // --- 父路由：承载所有 era-lite 页面的布局 ---
        path: '/era-lite',
        // 动态导入我们的主布局组件
        component: () => import('./layouts/MainLayout.vue'),
        // 当用户访问 /era-lite 时，自动重定向到主菜单页面
        redirect: { name: 'Home' },

        // --- 子路由：所有页面都在 MainLayout 的 <router-view> 中显示 ---
        children: [
            {
                // 路径为空，匹配 /era-lite
                path: 'home', // 明确路径为 home, 完整路径 /era-lite/home
                name: 'Home', // 必须与 MainLayout.vue 中的 key 匹配
                component: () => import('./views/HomeView.vue'),
                meta: {
                    title: '主菜单',
                }
            },
            {
                path: 'characters', // 完整路径 /era-lite/characters
                name: 'Characters',
                component: () => import('./views/CharacterListView.vue'),
                meta: {
                    title: '角色列表',
                }
            },
            {
                path: 'scenes', // 完整路径 /era-lite/scenes
                name: 'Scenes',
                component: () => import('./views/SceneListView.vue'),
                meta: {
                    title: '场景列表',
                }
            },
            {
                path: 'shop', // 完整路径 /era-lite/shop
                name: 'Shop',
                component: () => import('./views/ShopView.vue'),
                meta: {
                    title: '道具商店',
                }
            },
            {
                path: 'backpack', // 完整路径 /era-lite/backpack
                name: 'Backpack',
                component: () => import('./views/BackpackView.vue'),
                meta: {
                    title: '我的背包',
                }
            },
        ]
    },
];