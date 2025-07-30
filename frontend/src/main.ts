// src/main.ts (示例)
import {createApp} from 'vue';
import {createPinia} from 'pinia';
import App from './App.vue';
import {OpenAPI} from './app-game/types/generated/public-api-client'; // 确认路径正确
// 通用字体
import 'vfonts/Lato.css'
// 等宽字体
import 'vfonts/FiraCode.css'
import router from './router'
import 'vue-virtual-scroller/dist/vue-virtual-scroller.css'; // Import base CSS
import './styles/draggable.css';
// @ts-ignore
import VueVirtualScroller from 'vue-virtual-scroller'


import {
    create,
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
    // ... 可能还有其他，可以根据后续警告补充
} from 'naive-ui'
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



// 配置后端 API 的基础 URL
// 通常从环境变量读取，例如 Vite 的 import.meta.env
OpenAPI.BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7018'; // 替换为你的实际后端地址

const app = createApp(App);

app.use(router)
app.use(naiveForVueForm)
app.use(VueVirtualScroller)
app.use(createPinia())
// --- 将 Token 注入到所有 API 请求中 ---
// 必须在 Pinia 安装之后，才能使用 useAuthStore
// 导入所有需要认证的 API 客户端的 OpenAPI 对象
import { OpenAPI as AuthApiClient } from '@/app-authentication/types/generated/authentication-api-client';
import { OpenAPI as AiConfigApiClient } from '@/app-workbench/types/generated/ai-config-api-client';
import { OpenAPI as WorkflowConfigApiClient } from '@/app-workbench/types/generated/workflow-config-api-client';
import { OpenAPI as WorkflowTestApiClient } from '@/app-test-harness/types/generated/workflow-test-api-client';
import { OpenAPI as PublicApiClient } from '@/app-game/types/generated/public-api-client';
import {useAuthStore} from "@/app-authentication/stores/authStore.ts";
// 导入 ApiRequestOptions 类型，我们可以从任何一个客户端导入，因为它们是相同的
import type { ApiRequestOptions } from '@/app-workbench/types/generated/workflow-config-api-client/core/ApiRequestOptions';
const authStore = useAuthStore();
// 定义一个函数，用于从 store 中获取 token
/**
 * 创建一个完全符合 openapi-typescript-codegen Resolver<string> 类型的 Token 解析器。
 *
 * @param options - ApiRequestOptions 对象。此参数由类型签名要求，即使在此处未使用。
 * @returns {Promise<string>} 一个解析为认证 Token 的 Promise。如果 Token 不存在，则解析为空字符串。
 */
const tokenResolver = async (options: ApiRequestOptions): Promise<string> => {
    // console.log('Resolving token for request:', options.url); // 用于调试
    return authStore.token ?? ''; // 关键：如果 token 是 null/undefined，则返回 ''
};

// 为每一个 API 客户端设置 TOKEN 解析器
AuthApiClient.TOKEN = tokenResolver;
AiConfigApiClient.TOKEN = tokenResolver;
WorkflowConfigApiClient.TOKEN = tokenResolver;
WorkflowTestApiClient.TOKEN = tokenResolver;
PublicApiClient.TOKEN = tokenResolver;
// 如果未来有更多客户端，也在这里添加


app.mount('#app')

// 可以在这里或 App.vue 的 onMounted 中初始化 SignalR 连接
// import { useNarrativeStore } from './stores/narrativeStore';
// const narrativeStore = useNarrativeStore();
// narrativeStore.connectSignalR();