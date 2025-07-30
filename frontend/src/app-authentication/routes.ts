// src/app-authentication/routes.ts
import type {RouteRecordRaw} from 'vue-router';
import LoginTestView from './LoginTestView.vue';

export const routes: RouteRecordRaw[] = [
    // {
    //     path: '/login',
    //     name: 'Login',
    //     component: LoginView,
    //     meta: {
    //         // 标记这个页面不需要认证
    //         requiresAuth: false,
    //     },
    // },
    {
        path: '/login', // 定义一个专门的URL
        name: 'Login',
        component: LoginTestView,
        meta: {
            requiresAuth: false,
        }
    }
];