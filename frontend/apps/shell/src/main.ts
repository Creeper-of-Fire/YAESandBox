// src/main.ts
import * as Vue from 'vue';
import {createApp} from 'vue';
import {createPinia} from 'pinia';
import App from './App.vue';
// 通用字体
import 'vfonts/Lato.css'
// 等宽字体
import 'vfonts/FiraCode.css'
import router from './router/routerIndex.ts'
import './styles/draggable.css';


import {
    create,
    NCheckbox,
    NDatePicker,
    NForm,
    NFormItem,
    NInput,
    NInputNumber,
    NPopover,
    NRadio,
    NRadioGroup,
    NSelect,
    NSlider,
    NSwitch,
    NTimePicker,
} from 'naive-ui'
import {installBuiltinComponents} from "@yaesandbox-frontend/shared-ui/content-renderer";
// 导入 ApiRequestOptions 类型，我们可以从任何一个客户端导入，因为它们是相同的
// --- 将 Token 注入到所有 API 请求中 ---
// 必须在 Pinia 安装之后，才能使用 useAuthStore
// 导入所有需要认证的 API 客户端的 OpenAPI 对象
import {OpenAPI as AuthApiClient} from '#/app-authentication/types/generated/authentication-api-client'
import {useAuthStore} from "#/app-authentication/stores/authStore.ts"
import {type ApiRequestOptions, TokenResolverKey} from '@yaesandbox-frontend/core-services/injectKeys';
import {loadPlugins} from "#/plugins/pluginLoader.ts";
// 创建一个专门给 vue-form 用的 naive-ui 实例
const naiveForVueForm = create({
    components: [
        NForm,
        NFormItem,
        NInput,
        NInputNumber,
        NSelect,
        NCheckbox,
        NRadio,
        NRadioGroup,
        NSlider,
        NSwitch,
        NDatePicker,
        NTimePicker,
        NPopover,
    ]
})

const app = createApp(App);

// 调用插件加载器
const loadedPluginMetas = await loadPlugins(app);
// 将插件元数据 provide 给整个应用，以便 App.vue 生成导航
app.provide('loadedPlugins', loadedPluginMetas);

app.use(router)
app.use(naiveForVueForm)
app.use(createPinia())


// 定义一个函数，用于从 store 中获取 token
/**
 * 创建一个完全符合 openapi-typescript-codegen Resolver<string> 类型的 Token 解析器。
 *
 * @param options - ApiRequestOptions 对象。此参数由类型签名要求，即使在此处未使用。
 * @returns {Promise<string>} 一个解析为认证 Token 的 Promise。如果 Token 不存在，则解析为空字符串。
 */
const tokenResolver = async (options: ApiRequestOptions): Promise<string> =>
{
    // 在函数内部调用 useAuthStore
    const authStore = useAuthStore();

    // 如果 token 本身就不存在，直接返回空字符串
    if (!authStore.token) {
        return '';
    }

    // 检查 token 是否即将过期 (例如，在 60 秒内)
    if (authStore.isTokenExpiring(60)) {
        console.debug('Token 即将/已经过期，准备进行刷新。');
        // 调用刷新逻辑
        await authStore.refreshAccessToken();
    }

    // 返回当前（可能已刷新）的 token
    return authStore.token ?? '';
};
app.provide(TokenResolverKey, tokenResolver);

installBuiltinComponents();

app.mount('#app')

// ---  为插件暴露全局依赖 ---
// 这段代码是专门为了让插件能够找到Vue
// @ts-ignore
window.Vue = Vue;
// ------------------------------------