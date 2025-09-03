import type {RouteRecordRaw} from 'vue-router';

export const routes: RouteRecordRaw[] = [
    {
        // --- 新的启动页路由 ---
        path: '/era-lite',
        name: 'Era_Lite_Startup',
        component: () => import('#/features/home/StartupView.vue'),
        meta: {
            // 标记这个路由不需要激活的存档
            requiresActiveSave: false,
        }
    },
    {
        // --- 父路由：承载所有游戏内页面的布局 ---
        path: '/era-lite/session', // 路径已更改
        // 动态导入我们的主布局组件
        component: () => import('./layouts/MainLayout.vue'),
        meta: {
            // 标记所有子路由都需要一个激活的存档
            requiresActiveSave: true,
        },

        // --- 子路由：所有页面都在 MainLayout 的 <router-view> 中显示 ---
        children: [
            {
                // 路径为空，匹配 /era-lite
                path: '', // 使其成为 /era-lite 的默认子路由
                name: 'Era_Lite_Home',
                component: () => import('#/features/home/HomeView.vue'),
                meta: {
                    title: '主菜单',
                }
            },
            {
                // 为 'home' 路径添加一个别名
                path: 'home',
                redirect: {name: 'Era_Lite_Home'},
            },
            {
                path: 'characters', // 完整路径 /era-lite/characters
                name: 'Era_Lite_Characters',
                component: () => import('#/features/characters/CharacterListView.vue'),
                meta: {
                    title: '角色列表',
                }
            },
            {
                path: 'scenes', // 完整路径 /era-lite/scenes
                name: 'Era_Lite_Scenes',
                component: () => import('#/features/scenes/SceneListView.vue'),
                meta: {
                    title: '场景列表',
                }
            },
            {
                path: 'shop', // 完整路径 /era-lite/shop
                name: 'Era_Lite_Shop',
                component: () => import('#/features/shop/ShopView.vue'),
                meta: {
                    title: '道具商店',
                }
            },
            {
                path: 'backpack', // 完整路径 /era-lite/backpack
                name: 'Era_Lite_Backpack',
                component: () => import('#/features/backpack/BackpackView.vue'),
                meta: {
                    title: '我的背包',
                }
            },
            {
                path: 'chat', // 聊天列表
                name: 'Era_Lite_Chat_List',
                component: () => import('#/features/chat/ChatListView.vue'),
                meta: {
                    title: '对话记录'
                }
            },
            {
                path: 'chat/:sessionId', // 单个聊天视图
                name: 'Era_Lite_Chat_View',
                component: () => import('#/features/chat/ChatView.vue'),
                meta: {
                    title: '对话'
                }
            }
        ]
    },
];