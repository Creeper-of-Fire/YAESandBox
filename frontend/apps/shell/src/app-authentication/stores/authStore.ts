// src/app-authentication/authStore.ts
import {defineStore} from 'pinia';
import {computed, ref} from 'vue';
import {
    type AuthResponse,
    AuthService,
    type LoginRequest,
    type RegisterRequest
} from '#/app-authentication/types/generated/authentication-api-client';
import {useStorage} from "@vueuse/core";
import router from "#/router/routerIndex.ts";
import {jwtDecode} from 'jwt-decode';

const serializer = {
    read: (v: any) => v ? JSON.parse(v) : null,
    write: (v: any) => JSON.stringify(v),
}

export const useAuthStore = defineStore('authentication', () =>
{
    // --- State ---
    const token = useStorage<string | null>('authToken', null, localStorage);
    const refreshToken = useStorage<string | null>('authRefreshToken', null, localStorage);
    const user = useStorage<{ userId: string, username: string } | null>('authUser', null, localStorage, {serializer});
    const authError = ref<string | null>(null);
    const userName = computed(() => user.value?.username || '');

    const isRefreshing = ref(false); // 防止并发刷新

    // --- Getters ---
    const isAuthenticated = computed(() => !!token.value && !!user.value);

    // --- Actions ---
    /**
     * 设置认证信息
     */
    function setAuth(authResponse: AuthResponse)
    {
        token.value = authResponse.token;
        refreshToken.value = authResponse.refreshToken;
        user.value = {userId: authResponse.userId, username: authResponse.username};
    }

    /**
     * 清除认证信息
     */
    function clearAuth()
    {
        token.value = null;
        refreshToken.value = null;
        user.value = null;
    }


    /**
     * 登录
     * @param credentials - 用户名和密码
     */
    async function login(credentials: LoginRequest): Promise<boolean>
    {
        authError.value = null;
        try
        {
            const response = await AuthService.postApiV1AuthLogin({requestBody: credentials});

            setAuth(response);

            // // --- 优雅的改动 ---
            // // 检查路由中是否有 'redirect' 参数，实现登录后跳转回原页面的功能
            // const urlParams = new URLSearchParams(window.location.search);
            // // 默认重定向到根目录
            // window.location.href = urlParams.get('redirect') || '/'; // 跳转并刷新
            const redirectPath = router.currentRoute.value.query.redirect as string || '/';
            await router.push(redirectPath);

            return true; // 返回成功
        } catch (error: any)
        {
            console.error("Login failed:", error);
            authError.value = error.body?.message || error.message || '登录失败，请检查用户名或密码。';
            logout(false);
            return false; // 返回失败
        }
    }

    /**
     * 刷新 Access Token
     */
    async function refreshAccessToken(): Promise<string | null>
    {
        const currentRefreshToken = refreshToken.value;
        const currentUserId = user.value?.userId;

        // 如果缺少刷新令牌或用户信息，则无法刷新，直接登出
        if (!currentRefreshToken || !currentUserId)
        {
            console.warn("没有刷新令牌或用户信息，无法刷新。");
            logout();
            return null;
        }

        // 如果正在刷新，则等待并返回结果，避免并发
        if (isRefreshing.value)
        {
            // 等待刷新完成
            await new Promise(resolve =>
            {
                const interval = setInterval(() =>
                {
                    if (!isRefreshing.value)
                    {
                        clearInterval(interval);
                        resolve(true);
                    }
                }, 100);
            });
            return token.value;
        }

        isRefreshing.value = true;
        try
        {
            const response = await AuthService.postApiV1AuthRefresh({
                requestBody: {
                    userId: currentUserId,
                    refreshToken: currentRefreshToken
                }
            });
            setAuth(response);
            return response.token;
        } catch (error)
        {
            console.error("Failed to refresh token, logging out.", error);
            logout(); // 刷新失败，强制登出
            return null;
        } finally
        {
            isRefreshing.value = false;
        }
    }

    /**
     * 检查 token 是否即将过期
     * @param offsetSeconds - 提前多少秒认为即将过期，默认为 60 秒
     * @returns boolean
     */
    function isTokenExpiring(offsetSeconds = 60): boolean
    {
        if (!token.value) return false;
        try
        {
            const decoded = jwtDecode(token.value);
            const expiresAt = (decoded.exp ?? 0) * 1000; // exp 是秒，转换为毫秒
            const now = Date.now();
            return expiresAt < now + offsetSeconds * 1000;
        } catch (e)
        {
            console.error("Failed to decode token", e);
            return true; // 解码失败，视为已过期
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
        clearAuth();

        if (redirect)
        {
            window.location.href = '/login'; // 假设登录页的路径是 /login
        }
    }

    return {
        token,
        refreshToken,
        user,
        userName,
        isAuthenticated,
        authError,
        isTokenExpiring,
        refreshAccessToken,
        login,
        register,
        logout,
    };
});