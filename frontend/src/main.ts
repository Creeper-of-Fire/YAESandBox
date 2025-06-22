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
const pinia = createPinia();

app.use(pinia)
app.use(router)
app.use(naiveForVueForm)
app.use(VueVirtualScroller)
app.mount('#app')

// 可以在这里或 App.vue 的 onMounted 中初始化 SignalR 连接
// import { useNarrativeStore } from './stores/narrativeStore';
// const narrativeStore = useNarrativeStore();
// narrativeStore.connectSignalR();