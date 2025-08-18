// src/app-authentication/authStore.ts
import {defineStore} from 'pinia';
import {computed, ref} from 'vue';
import {
    type AuthResponse,
    AuthService,
    type LoginRequest,
    type RegisterRequest
} from '@/app-authentication/types/generated/authentication-api-client';
import {useStorage} from "@vueuse/core";

export const useAuthStore = defineStore('authentication', () =>
{
    // --- State ---
    const token = useStorage<string | null>('authToken', null);
    const user = useStorage<Pick<AuthResponse, 'userId' | 'username'> | null>('authUser', null);
    const authError = ref<string | null>(null);

    // --- Getters ---
    const isAuthenticated = computed(() => !!token.value);

    // --- Actions ---

    /**
     * 登录
     * @param credentials - 用户名和密码
     */
    async function login(credentials: LoginRequest): Promise<boolean> {
        authError.value = null;
        try {
            const response = await AuthService.postApiV1AuthLogin({ requestBody: credentials });

            // 登录成功
            token.value = response.token!;
            user.value = { userId: response.userId, username: response.username };

            // --- 优雅的改动 ---
            // 检查路由中是否有 'redirect' 参数，实现登录后跳转回原页面的功能
            const urlParams = new URLSearchParams(window.location.search);
             // 默认重定向到根目录
            window.location.href = urlParams.get('redirect') || '/'; // 跳转并刷新

            return true; // 返回成功
        } catch (error: any) {
            console.error("Login failed:", error);
            authError.value = error.body?.message || error.message || '登录失败，请检查用户名或密码。';
            logout(false);
            return false; // 返回失败
        }
    }

    /**
     * 处理用户注册逻辑
     * @param credentials - 注册所需的用户信息（包含用户名、密码等，具体字段由 RegisterRequest 类型定义）
     * @returns Promise<boolean> - 注册成功返回 true，失败返回 false
     */
    async function register(credentials: RegisterRequest): Promise<boolean>
    {
        authError.value = null;
        try
        {
            const successMessage = await AuthService.postApiV1AuthRegister({requestBody: credentials});
            console.log('注册成功:', successMessage);
            // 可以在这里用全局 message 提示用户注册成功
            // message.success(successMessage || '注册成功！现在可以登录了。');
            return true; // 表示成功
        } catch (error: any)
        {
            console.error("注册失败:", error);
            authError.value = error.body || error.message || '注册失败，用户名可能已被占用。';
            return false; // 表示失败
        }
    }

    /**
     * 登出
     * @param redirect - 是否重定向到登录页
     */
    function logout(redirect = true)
    {
        token.value = null;
        user.value = null;

        if (redirect)
        {
            window.location.href = '/login'; // 假设登录页的路径是 /login
        }
    }

    return {
        token,
        user,
        isAuthenticated,
        authError,
        login,
        register,
        logout,
    };
});