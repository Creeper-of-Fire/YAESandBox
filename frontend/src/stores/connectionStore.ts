// src/stores/connectionStore.ts
import {defineStore} from 'pinia';

interface ConnectionState {
    isSignalRConnected: boolean;
    isSignalRConnecting: boolean;
    // 可以添加其他全局连接状态，如 API 健康状态等
}

const defaultState: ConnectionState = {
    isSignalRConnected: false,
    isSignalRConnecting: false,
};

export const useConnectionStore = defineStore('connection', {
    state: (): ConnectionState => ({...defaultState}),

    getters: {
        // 提供 getter 方便组件访问
        getIsSignalRConnected(state): boolean {
            return state.isSignalRConnected;
        },
        getIsSignalRConnecting(state): boolean {
            return state.isSignalRConnecting;
        },
    },

    actions: {
        /**
         * 设置 SignalR 的连接状态。
         * 这个 action 由 signalrService 调用。
         */
        setSignalRConnectionStatus(isConnected: boolean, isConnecting: boolean) {
            this.isSignalRConnected = isConnected;
            this.isSignalRConnecting = isConnecting;
            console.log(`ConnectionStore: SignalR status updated - Connected: ${isConnected}, Connecting: ${isConnecting}`);
        },

        // 可以添加手动触发连接/断开的 action，这些 action 会调用 signalrService
        async connectSignalR() {
            // 导入 signalrService (注意避免循环依赖)
            const {signalrService} = await import('@/services/signalrService'); // 动态导入
            if (!this.isSignalRConnected && !this.isSignalRConnecting) {
                try {
                    // 调用 signalrService 的 start 方法
                    // 需要确保 OpenAPI.BASE 已配置
                    const {OpenAPI} = await import('@/types/generated/api/core/OpenAPI');
                    await signalrService.start(OpenAPI.BASE);
                } catch (error) {
                    console.error("ConnectionStore: connectSignalR failed", error);
                    // 状态更新由 signalrService 的回调处理
                }
            }
        },

        async disconnectSignalR() {
            const {signalrService} = await import('../services/signalrService'); // 动态导入
            if (this.isSignalRConnected) {
                await signalrService.stop();
                // 状态更新由 signalrService 的回调处理
            }
        }
    }
});