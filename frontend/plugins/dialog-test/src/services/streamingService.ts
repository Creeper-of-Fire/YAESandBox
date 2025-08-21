import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import type { WorkflowConfig } from "@yaesandbox-frontend/plugin-workbench/types/generated/workflow-config-api-client";
import type { ApiRequestOptions } from "@yaesandbox-frontend/core-services/injectKeys";

// 请求体的类型 (用于外部调用，保持不变)
interface StreamRequest
{
    workflowConfig: WorkflowConfig;
    workflowInputs: Record<string, string>;
}

// SignalR 服务器发送的消息负载类型 (与 C# 的 StreamMessage 对应)
interface ServerStreamMessage
{
    type: 'data' | 'error' | 'done';
    content?: string;
}

// 回调函数的接口 (保持不变)
interface StreamCallbacks
{
    onMessage: (content: string) => void;
    onError: (error: Error) => void;
    onClose: () => void;
}

// 封装的流式请求函数 (已修改为使用 SignalR)
export async function executeWorkflowStream(
    requestBody: StreamRequest,
    callbacks: StreamCallbacks,
    tokenResolver: (options: ApiRequestOptions) => Promise<string>
)
{
    let connection: HubConnection | null = null;

    const HUB_URL = '/hubs/game-era-test';
    try {
        // 1. 获取用于 SignalR Hub 连接的认证 Token
        const signalRToken = await tokenResolver({
            method: 'POST', // Method doesn't matter as much for the hub URL itself
            url: HUB_URL,
        });

        // 2. 构建并配置 SignalR 连接
        connection = new HubConnectionBuilder()
            .withUrl(HUB_URL, {
                // 使用 accessTokenFactory 来提供 token
                accessTokenFactory: () => signalRToken
            })
            .withAutomaticReconnect()
            .configureLogging(LogLevel.Information)
            .build();

        // 3. 定义客户端方法，用于接收来自服务器的推送
        connection.on("ReceiveWorkflowUpdate", (message: ServerStreamMessage) => {
            switch (message.type)
            {
                case 'data':
                    callbacks.onMessage(message.content || '');
                    break;
                case 'error':
                    callbacks.onError(new Error(message.content || 'Unknown stream error'));
                    // 收到错误后，可以主动关闭连接
                    connection?.stop();
                    break;
                case 'done':
                    callbacks.onClose();
                    // 任务完成，主动关闭连接
                    connection?.stop();
                    break;
            }
        });

        // 4. 定义连接关闭时的处理逻辑
        connection.onclose((error:any) => {
            console.log('SignalR connection closed.');
            if (error)
            {
                // 只有在异常关闭时才调用 onError，正常关闭由 'done' 消息处理
                callbacks.onError(new Error(`Connection closed with error: ${error}`));
            }
        });

        // 5. 启动连接
        await connection.start();
        console.log('SignalR connected with connectionId:', connection.connectionId);

        // 确保我们获得了 connectionId
        if (!connection.connectionId)
        {
            throw new Error('Failed to establish SignalR connection: No connection ID.');
        }

        // 6. 连接成功后，发送 HTTP 请求以触发后台工作流
        const triggerToken = await tokenResolver({
            method: 'POST',
            url: '/api/v1/workflow-execution/execute-signalr',
        });

        // 构建 headers 对象
        const headers: Record<string, string> = {
            'Content-Type': 'application/json',
        };
        if (triggerToken)
        {
            headers['Authorization'] = `Bearer ${triggerToken}`;
        }

        const triggerResponse = await fetch('/api/v1/workflow-execution/execute-signalr', {
            method: 'POST',
            headers: headers,
            body: JSON.stringify({
                ...requestBody,
                connectionId: connection.connectionId, // 附加 connectionId
            }),
        });

        // 如果触发请求失败 (不是 202 Accepted)，则这是一个错误
        if (triggerResponse.status !== 202)
        {
            const errorText = await triggerResponse.text();
            throw new Error(`Failed to trigger workflow. Status: ${triggerResponse.status}. Body: ${errorText}`);
        }

    } catch (err) {
        // 捕获设置或连接过程中的任何错误
        callbacks.onError(err as Error);
        // 确保在出错时清理连接
        if (connection && connection.state === 'Connected') {
            await connection.stop();
        }
        // 返回一个无效的中止函数
        return { abort: () => {} };
    }

    // 7. 返回一个可以从外部调用的中止函数
    // `connection.stop()` 会优雅地关闭连接
    const activeConnection = connection;
    return {
        abort: () => {
            console.log('Aborting workflow by stopping SignalR connection.');
            activeConnection?.stop();
        },
    };
}