import type {RouteRecordRaw} from 'vue-router';

export const routes: RouteRecordRaw[] = [
    {
        // 插件的根路径，组件是我们的控制中心 PluginRoot.vue
        path: '/era-lite',
        component: () => import('#/PluginRoot.vue'),

        // 当 PluginRoot 中的 v-if 为 true (有存档) 时，
        // <router-view> 将会从这里的 children 中寻找匹配项来渲染。
        children: [
            {
                // 这个父路由专门用来加载 MainLayout 布局
                path: '', // 匹配 /era-lite
                component: () => import('#/layouts/MainLayout.vue'),

                // 所有游戏内页面都成为 MainLayout 的子路由
                // 这样它们就会被渲染到 MainLayout 的 <router-view> 中
                children: [
                    {
                        path: '', // 默认子路由，匹配 /era-lite
                        name: 'Era_Lite_Home',
                        component: () => import('#/features/home/HomeView.vue'),
                        meta: { title: '主菜单' }
                    },
                    {
                        path: 'home', // 别名
                        redirect: { name: 'Era_Lite_Home' },
                    },
                    {
                        path: 'characters',
                        name: 'Era_Lite_Characters',
                        component: () => import('#/features/characters/CharacterListView.vue'),
                        meta: { title: '角色列表' }
                    },
                    {
                        path: 'scenes',
                        name: 'Era_Lite_Scenes',
                        component: () => import('#/features/scenes/SceneListView.vue'),
                        meta: { title: '场景列表' }
                    },
                    {
                        path: 'shop',
                        name: 'Era_Lite_Shop',
                        component: () => import('#/features/shop/ShopView.vue'),
                        meta: { title: '道具商店' }
                    },
                    {
                        path: 'backpack',
                        name: 'Era_Lite_Backpack',
                        component: () => import('#/features/backpack/BackpackView.vue'),
                        meta: { title: '我的背包' }
                    },
                    {
                        path: 'chat',
                        name: 'Era_Lite_Chat_List',
                        component: () => import('#/features/chat/ChatListView.vue'),
                        meta: { title: '对话记录' }
                    },
                    {
                        path: 'chat/:sessionId',
                        name: 'Era_Lite_Chat_View',
                        component: () => import('#/features/chat/ChatView.vue'),
                        meta: { title: '对话' }
                    }
                ]
            }
        ]
    }
];