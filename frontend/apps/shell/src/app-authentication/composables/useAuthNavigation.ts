import {useRoute, useRouter} from "vue-router";
import {useAuthStore} from "#/app-authentication/stores/authStore.ts";
import type {LoginRequest, RegisterRequest} from "#/app-authentication/types/generated/authentication-api-client";
import {useMessage} from "naive-ui";

/**
 * 一个封装了认证相关导航逻辑的 Composable。
 */
export function useAuthNavigation()
{
    const router = useRouter();
    const route = useRoute();
    const authStore = useAuthStore();
    const message = useMessage();

    /**
     * 处理登录流程，包括调用 store 和成功后的导航。
     * @param credentials - 登录凭据
     * @returns {Promise<boolean>} - 登录是否成功
     */
    async function loginAndRedirect(credentials: LoginRequest): Promise<boolean>
    {
        const loginResult = await authStore.login(credentials);

        if (loginResult.IsSuccess)
        {
            // 登录成功，执行跳转
            const redirectPath = route.query.redirect as string || '/';
            await router.push(redirectPath);
            return true;
        }
        message.error(loginResult.ErrorMessage);
        return false;
    }

    /**
     * 处理登出流程，包括调用 store 和导航到登录页。
     */
    function logoutAndRedirect()
    {
        authStore.logout();
        router.push('/login');
    }

    /**
     * 处理注册流程，包括调用 store 和成功后的导航。
     * @param credentials - 注册凭据
     * @returns {Promise<boolean>} - 注册是否成功
     */
    async function registerAndRedirect(credentials: RegisterRequest): Promise<boolean>
    {
        const registerResult = await authStore.register(credentials);

        if (registerResult.IsSuccess)
        {
            // 注册成功后，通常会跳转到登录页面，并可能带上刚刚注册的用户名
            await router.push({name: 'Login', query: {username: credentials.username}});
            // 这里你也可以选择直接跳转到其他页面，比如一个“注册成功，请检查邮箱”的提示页
            return true;
        }
        message.error(registerResult.ErrorMessage);
        return false;
    }

    /**
     * 处理“登录或注册”的复合流程。
     * @param credentials - 包含用户名和密码的凭据
     * @returns {Promise<boolean>} - 最终操作是否成功
     */
    async function loginOrRegisterAndRedirect(credentials: LoginRequest): Promise<boolean>
    {
        // 步骤 1: 尝试登录
        const loginResult = await authStore.login(credentials);

        if (loginResult.IsSuccess)
        {
            // 登录成功，直接跳转
            const redirectPath = route.query.redirect as string || '/';
            await router.push(redirectPath);
            return true;
        }
        // 步骤 2: 登录失败，尝试注册
        console.log("Login attempt failed, attempting to register...");
        const registerResult = await authStore.register({
            username: credentials.username,
            password: credentials.password,
            // 如果注册需要其他字段，可以在这里传入默认值或扩展参数
        });

        if (registerResult.IsSuccess)
        {
            message.success('新测试用户注册成功！正在自动登录...');
            // 步骤 3: 注册成功后再次尝试登录
            const finalLoginSuccess = await authStore.login(credentials);
            if (finalLoginSuccess.IsSuccess)
            {
                const redirectPath = route.query.redirect as string || '/';
                await router.push(redirectPath);
                return true;
            }
            message.error('注册成功后登录失败');
        }

        // 如果所有步骤都失败了
        message.error('登录失败，注册失败');
        return false;
    }

    return {
        loginAndRedirect,
        logoutAndRedirect,
        registerAndRedirect,
        loginOrRegisterAndRedirect
    };
}