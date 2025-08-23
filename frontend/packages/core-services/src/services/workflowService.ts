// @yaesandbox-frontend/core-services/services/workflowService.ts
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
// 假设这些类型可以从你的项目别名中导入
import type { ApiRequestOptions } from "../utils/injectKeys.ts";
import type {WorkflowConfig} from "../types";

// 请求体的类型 (用于外部调用，保持不变)
export interface StreamRequest {
    workflowConfig: WorkflowConfig;
    workflowInputs: Record<string, string>;
}

// SignalR 服务器发送的消息负载类型 (与 C# 的 StreamMessage 对应)
interface ServerStreamMessage {
    type: 'data' | 'error' | 'done';
    content?: string;
}

// 回调函数的接口 (保持不变)
export interface StreamCallbacks {
    onMessage: (content: string) => void;
    onError: (error: Error) => void;
    onClose: () => void;
}

/**
 * 封装的流式请求函数，用于执行后端工作流并通过 SignalR 接收实时更新。
 * @param requestBody 包含工作流配置和输入的请求。
 * @param callbacks 用于处理流消息、错误和关闭事件的回调。
 * @param tokenResolver 一个函数，用于获取 API 请求所需的认证 token。
 * @param outputFormat 指定从后端请求的数据格式，默认为 'Json'。
 * @returns 返回一个包含 abort 方法的对象，可用于中止流式请求。
 */
export async function executeWorkflowStream(
    requestBody: StreamRequest,
    callbacks: StreamCallbacks,
    tokenResolver: (options: ApiRequestOptions) => Promise<string>,
    outputFormat: 'Json' | 'Xml' = 'Json'
) {
    let connection: HubConnection | null = null;
    const HUB_URL = '/hubs/game-era-test';

    try {
        // 1. 获取用于 SignalR Hub 连接的认证 Token
        const signalRToken = await tokenResolver({
            method: 'POST',
            url: HUB_URL,
        });

        // 2. 构建并配置 SignalR 连接
        connection = new HubConnectionBuilder()
            .withUrl(HUB_URL, {
                accessTokenFactory: () => signalRToken
            })
            .withAutomaticReconnect()
            .configureLogging(LogLevel.Information)
            .build();

        // 3. 定义客户端方法，用于接收来自服务器的推送
        connection.on("ReceiveWorkflowUpdate", (message: ServerStreamMessage) => {
            switch (message.type) {
                case 'data':
                    callbacks.onMessage(message.content || '');
                    break;
                case 'error':
                    callbacks.onError(new Error(message.content || 'Unknown stream error'));
                    connection?.stop();
                    break;
                case 'done':
                    // 'done' 消息表示成功完成，调用 onClose 回调
                    callbacks.onClose();
                    connection?.stop();
                    break;
            }
        });

        // 4. 定义连接关闭时的处理逻辑
        connection.onclose((error) => {
            console.log('SignalR connection closed.');
            if (error) {
                // 只有在异常关闭时才调用 onError。正常关闭由 'done' 消息触发 onClose
                callbacks.onError(new Error(`Connection closed with error: ${error}`));
            } else {
                // 如果是正常关闭（例如服务器或客户端调用 stop() 且没有错误），
                // 也可以触发 onClose 以确保状态被清理
                callbacks.onClose();
            }
        });

        // 5. 启动连接
        await connection.start();
        console.log('SignalR connected with connectionId:', connection.connectionId);

        if (!connection.connectionId) {
            throw new Error('Failed to establish SignalR connection: No connection ID.');
        }

        // 6. 连接成功后，发送 HTTP 请求以触发后台工作流
        const triggerToken = await tokenResolver({
            method: 'POST',
            url: '/api/v1/workflow-execution/execute-signalr',
        });

        const headers: Record<string, string> = {
            'Content-Type': 'application/json',
        };
        if (triggerToken) {
            headers['Authorization'] = `Bearer ${triggerToken}`;
        }

        const triggerResponse = await fetch('/api/v1/workflow-execution/execute-signalr', {
            method: 'POST',
            headers: headers,
            body: JSON.stringify({
                ...requestBody,
                connectionId: connection.connectionId,
                outputFormat: outputFormat, // <-- 将格式参数包含在请求体中
            }),
        });

        if (triggerResponse.status !== 202) {
            const errorText = await triggerResponse.text();
            throw new Error(`Failed to trigger workflow. Status: ${triggerResponse.status}. Body: ${errorText}`);
        }

    } catch (err) {
        callbacks.onError(err as Error);
        if (connection && connection.state === 'Connected') {
            await connection.stop();
        }
        return { abort: () => {} };
    }

    // 7. 返回一个可以从外部调用的中止函数
    const activeConnection = connection;
    return {
        abort: () => {
            console.log('Aborting workflow by stopping SignalR connection.');
            activeConnection?.stop();
        },
    };
}