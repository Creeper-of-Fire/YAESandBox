import { fetchEventSource } from '@microsoft/fetch-event-source';
import type { WorkflowProcessorConfig } from "@/app-workbench/types/generated/workflow-config-api-client";
import { useAuthStore } from "@/app-authentication/stores/authStore";

// 请求体的类型
interface StreamRequest {
    workflowConfig: WorkflowProcessorConfig;
    triggerParams: Record<string, string>;
}

// SSE 事件的负载类型
interface StreamEventPayload {
    type: 'data' | 'error' | 'done';
    content?: string;
}

// 回调函数的接口
interface StreamCallbacks {
    onMessage: (content: string) => void;
    onError: (error: Error) => void;
    onClose: () => void;
}

// 封装的流式请求函数
export function executeWorkflowStream(requestBody: StreamRequest, callbacks: StreamCallbacks) {
    const authStore = useAuthStore();
    const token = authStore.token;

    const ctrl = new AbortController();

    // 构建 headers 对象，并动态添加 Authorization
    const headers: Record<string, string> = {
        'Content-Type': 'application/json',
        'Accept': 'text/event-stream',
    };

    if (token) {
        headers['Authorization'] = `Bearer ${token}`;
    }

    fetchEventSource('/api/v1/workflow-execution/execute-stream', {
        method: 'POST',
        headers: headers,
        body: JSON.stringify(requestBody),
        signal: ctrl.signal,

        async onopen(response) {
            if (response.ok && response.headers.get('content-type') === 'text/event-stream') {
                return; // 连接成功
            }
            throw new Error(`Failed to connect. Status: ${response.status} ${response.statusText}`);
        },

        onmessage(event) {
            const payload: StreamEventPayload = JSON.parse(event.data);

            switch (payload.type) {
                case 'data':
                    callbacks.onMessage(payload.content || '');
                    break;
                case 'error':
                    callbacks.onError(new Error(payload.content || 'Unknown stream error'));
                    break;
                case 'done':
                    // 后端发送 'done' 信号，我们在这里正常关闭
                    callbacks.onClose();
                    ctrl.abort(); // 主动关闭连接
                    break;
            }
        },

        onclose() {
            // 这个回调在连接被任何一方关闭时都会触发
            console.log('Stream connection closed.');
        },

        onerror(err) {
            // 这个回调处理网络层面的错误或 onopen 抛出的异常
            callbacks.onError(err);
            throw err; // 抛出错误以停止重连
        },
    });

    // 返回一个可以从外部调用的中止函数
    return {
        abort: () => ctrl.abort(),
    };
}