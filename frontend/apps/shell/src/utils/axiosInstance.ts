import axios from 'axios';
import { useAuthStore } from '#/app-authentication/stores/authStore';

// 1. 创建一个 axios 实例
const axiosInstance = axios.create({
    // 从环境变量中读取 API 基础 URL，与 OpenAPI 客户端保持一致
    baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:7018',
    headers: {
        'Content-Type': 'application/json',
    },
});

// 2. 添加请求拦截器 (这是关键)
axiosInstance.interceptors.request.use(
    (config) => {
        // 在发送请求之前，从 Pinia store 中获取 token
        // 注意：这里我们不能在顶层作用域调用 useAuthStore()，必须在拦截器函数内部
        const authStore = useAuthStore();
        const token = authStore.token;

        // 如果 token 存在，则将其添加到 Authorization 请求头中
        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }

        return config;
    },
    (error) => {
        // 对请求错误做些什么
        return Promise.reject(error);
    }
);

// 3. （可选）添加响应拦截器
axiosInstance.interceptors.response.use(
    (response) => {
        // 2xx 范围内的状态码都会触发该函数。
        // 对响应数据做点什么
        return response;
    },
    (error) => {
        // 超出 2xx 范围的状态码都会触发该函数。
        // 例如，如果收到 401 Unauthorized，可以触发登出逻辑
        if (error.response?.status === 401) {
            console.error("Axios Interceptor: Received 401, logging out.");
            const authStore = useAuthStore();
            authStore.logout();
        }
        return Promise.reject(error);
    }
);

// 4. 导出配置好的实例
export default axiosInstance;