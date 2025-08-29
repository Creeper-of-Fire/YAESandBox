import type { RouteRecordRaw } from 'vue-router';

export const routes: RouteRecordRaw[] = [
    {
        // --- 父路由：承载所有 era-lite 页面的布局 ---
        path: '/era-lite',
        // 动态导入我们的主布局组件
        component: () => import('./layouts/MainLayout.vue'),

        // --- 子路由：所有页面都在 MainLayout 的 <router-view> 中显示 ---
        children: [
            {
                // 路径为空，匹配 /era-lite
                path: '', // 使其成为 /era-lite 的默认子路由
                name: 'Era_Lite_Home',
                component: () => import('./fatures/home/HomeView.vue'),
                meta: {
                    title: '主菜单',
                }
            },
            {
                // 为 'home' 路径添加一个别名
                path: 'home',
                redirect: { name: 'Era_Lite_Home' },
            },
            {
                path: 'characters', // 完整路径 /era-lite/characters
                name: 'Era_Lite_Characters',
                component: () => import('./fatures/characters/CharacterListView.vue'),
                meta: {
                    title: '角色列表',
                }
            },
            {
                path: 'scenes', // 完整路径 /era-lite/scenes
                name: 'Era_Lite_Scenes',
                component: () => import('./fatures/scenes/SceneListView.vue'),
                meta: {
                    title: '场景列表',
                }
            },
            {
                path: 'shop', // 完整路径 /era-lite/shop
                name: 'Era_Lite_Shop',
                component: () => import('./fatures/shop/ShopView.vue'),
                meta: {
                    title: '道具商店',
                }
            },
            {
                path: 'backpack', // 完整路径 /era-lite/backpack
                name: 'Era_Lite_Backpack',
                component: () => import('./fatures/backpack/BackpackView.vue'),
                meta: {
                    title: '我的背包',
                }
            },
        ]
    },
];