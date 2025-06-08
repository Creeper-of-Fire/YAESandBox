// src/services/signalrService.ts
import * as signalR from "@microsoft/signalr";
import {OpenAPI} from '@/app-game/types/generated/public-api-client/core/OpenAPI.ts'; // 用于获取 BASE URL
import {useBlockStatusStore} from '@/app-game/features/block-bubble-stream-panel/blockStatusStore.ts';
import {useConnectionStore} from '@/app-game/stores/connectionStore.ts';
// import { useTopologyStore } from '../stores/topologyStore';
import {useBlockContentStore} from '@/app-game/features/block-bubble-stream-panel/blockContentStore.ts';
import {eventBus} from './eventBus.ts'; // 导入事件总线
import type {
    BlockStatusUpdateDto,
    BlockUpdateSignalDto,
    ConflictDetectedDto,
    DisplayUpdateDto,
    RegenerateBlockRequestDto,
    ResolveConflictRequestDto,
    TriggerMainWorkflowRequestDto,
    TriggerMicroWorkflowRequestDto,
    // Enums for event data payload
} from "@/app-game/types/generated/public-api-client";

import {StreamStatus, UpdateMode} from "@/app-game/types/generated/public-api-client";


// SignalR Hub 的相对路径
const HUB_URL = "/gamehub";

let connection: signalR.HubConnection | null = null;

// 不再需要全局 store 实例

/**
 * 延迟获取 BlockStatusStore 实例的函数。
 * 确保在 Pinia 初始化后调用。
 */
function getStatusStore()
{
    // 这里假设 Pinia 已经初始化
    // 在实际应用中，你可能需要在调用此函数前确保 Pinia 设置完成
    try
    {
        return useBlockStatusStore();
    } catch (error)
    {
        console.error("SignalR Service: 无法获取 Pinia Store (BlockStatusStore)。请确保 Pinia 已初始化。", error);
        // 返回一个模拟对象或抛出错误，防止后续代码出错
        throw new Error("Pinia store (BlockStatusStore) not available.");
        // 或者返回一个安全的空操作对象：
        // return { setSignalRConnectionStatus: () => {}, handleBlockStatusUpdate: () => {}, ... };
    }
}

function getBlockStore()
{
    // 这里假设 Pinia 已经初始化
    // 在实际应用中，你可能需要在调用此函数前确保 Pinia 设置完成
    try
    {
        return useBlockContentStore();
    } catch (error)
    {
        console.error("SignalR Service: 无法获取 Pinia Store (BlockContentStore)。请确保 Pinia 已初始化。", error);
        // 返回一个模拟对象或抛出错误，防止后续代码出错
        throw new Error("Pinia store (BlockContentStore) not available.");
        // 或者返回一个安全的空操作对象：
        // return { setSignalRConnectionStatus: () => {}, handleBlockStatusUpdate: () => {}, ... };
    }
}

// --- 辅助函数获取 Store ---
function getConnectionStore()
{
    try
    {
        return useConnectionStore();
    } catch (error)
    {
        console.error("SignalR Service: 无法获取 Pinia Store (ConnectionStore)。请确保 Pinia 已初始化。", error);
        throw new Error("Pinia store (ConnectionStore) not available.");
    }
}

/**
 * 启动 SignalR 连接
 * @param baseUrl 服务器的基础 URL (例如 http://localhost:5000)
 */
async function startConnection(baseUrl: string): Promise<void>
{
    const connectionStore = getConnectionStore(); // 获取一次 store 实例

    if (connection && connection.state === signalR.HubConnectionState.Connected)
    {
        console.log("SignalR 连接已存在。");
        return;
    }

    const hubUrl = `${baseUrl}${HUB_URL}`;
    console.log(`尝试连接到 SignalR Hub: ${hubUrl}`);

    // 如果已有连接但未连接，先停止
    if (connection)
    {
        try
        {
            await connection.stop();
        } catch (err)
        {
            console.error("停止旧 SignalR 连接时出错:", err);
        }
    }

    connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect({
            nextRetryDelayInMilliseconds: retryContext =>
            {
                const delay = Math.min(Math.pow(2, retryContext.previousRetryCount) * 1000, 60000);
                console.log(`SignalR 连接丢失，将在 ${delay / 1000} 秒后尝试重连 (${retryContext.previousRetryCount + 1} 次尝试)...`);
                return delay;
            }
        })
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // --- 注册服务器 -> 客户端的方法处理器 ---

    // Block 状态更新 -> BlockStatusStore
    connection.on("ReceiveBlockStatusUpdate", (data: BlockStatusUpdateDto) =>
    {
        console.log("SignalR: 收到 BlockStatusUpdate", data);
        try
        {
            getStatusStore().handleBlockStatusUpdate(data.blockId, data.statusCode);
        } catch (error)
        {
            console.error("SignalR: 处理 BlockStatusUpdate 时出错:", error);
        }
    });

    // 显示更新 -> 分发给 BlockStatusStore 或 EventBus
    connection.on("ReceiveDisplayUpdate", (data: DisplayUpdateDto) =>
    {
        console.log("SignalR: 收到 DisplayUpdate", data);
        try
        {
            if (data.targetElementId)
            {
                // --- 微工作流更新 -> EventBus ---
                const targetId = data.targetElementId;
                const eventName = `microWorkflowUpdate:${targetId}` as const;
                const eventData = {
                    content: data.content ?? null,
                    status: data.streamingStatus ?? StreamStatus.COMPLETE,
                    updateMode: data.updateMode ?? UpdateMode.FULL_SNAPSHOT, // 假设默认是 FullSnapshot
                    // 可以添加 requestId: data.requestId 等其他信息
                };
                console.log(`SignalR: 发布事件 ${eventName}`, eventData);
                eventBus.emit(eventName, eventData);
                // 由于微工作流也可能含有 contextBlockId，因此在这里截断。
                return;
            } else if (data.contextBlockId)
            {
                // --- 主流程/重新生成更新 -> BlockStatusStore ---
                if (data.streamingStatus === StreamStatus.COMPLETE)
                {
                    getBlockStore().fetchAllBlockDetails(data.contextBlockId);
                }
                getStatusStore().handleBlockDisplayUpdate(data);
            } else
            {
                console.warn("SignalR: 收到无效的 DisplayUpdate DTO (无 contextBlockId 或 targetElementId)", data);
            }
        } catch (error)
        {
            console.error("SignalR: 处理 DisplayUpdate 时出错:", error);
        }
    });

    // 冲突检测 -> BlockStatusStore
    connection.on("ReceiveConflictDetected", (blockId: string) =>
    {
        console.warn("SignalR: 收到 ConflictDetected", blockId);
        try
        {
            getStatusStore().handleConflictDetected(blockId);
        } catch (error)
        {
            console.error("SignalR: 处理 ConflictDetected 时出错:", blockId);
        }
    });

    // Block 更新信号 -> BlockStatusStore
    connection.on("ReceiveBlockUpdateSignal", (data: BlockUpdateSignalDto) =>
    {
        console.log("SignalR: 收到 BlockUpdateSignal", data);
        try
        {
            getStatusStore().handleBlockUpdateSignal(data);
        } catch (error)
        {
            console.error("SignalR: 处理 BlockUpdateSignal 时出错:", error);
        }
    });

    // --- 处理连接状态变化 ---

    connection.onreconnecting(error =>
    {
        console.warn(`SignalR 正在重连... 原因: ${error}`);
        try
        {
            // 调用 BlockStatusStore 更新全局连接状态
            connectionStore.setSignalRConnectionStatus(false, true); // isConnected: false, isConnecting: true
        } catch (storeError)
        {
            console.error("SignalR: 更新重连状态时出错:", storeError);
        }
    });

    connection.onreconnected(connectionId =>
    {
        console.log(`SignalR 重连成功！Connection ID: ${connectionId}`);
        try
        {
            connectionStore.setSignalRConnectionStatus(true, false); // isConnected: true, isConnecting: false
        } catch (storeError)
        {
            console.error("SignalR: 更新重连成功状态时出错:", storeError);
        }
    });

    connection.onclose(error =>
    {
        console.error(`SignalR 连接已关闭。原因: ${error}`);
        try
        {
            connectionStore.setSignalRConnectionStatus(false, false); // isConnected: false, isConnecting: false
        } catch (storeError)
        {
            console.error("SignalR: 更新关闭状态时出错:", storeError);
        }
        connection = null; // 清理连接对象
    });

    // --- 启动连接 ---
    try
    {
        connectionStore.setSignalRConnectionStatus(false, true);
        await connection.start();
        console.log("SignalR 连接成功！Connection ID:", connection.connectionId);
        connectionStore.setSignalRConnectionStatus(true, false);
    } catch (err)
    {
        console.error("SignalR 连接失败:", err);
        connectionStore.setSignalRConnectionStatus(false, false);
        connection = null;
        throw new Error(`无法连接到 SignalR Hub: ${err}`);
    }
}

/**
 * 停止 SignalR 连接
 */
async function stopConnection(): Promise<void>
{
    let connectionStore: ReturnType<typeof useConnectionStore> | null = null; // 获取 store 实例用于更新状态
    try
    {
        connectionStore = getConnectionStore();
    } catch (error)
    {
        console.error("SignalR Service: 停止连接时无法获取 BlockStatusStore。");
    }


    if (connection && connection.state !== signalR.HubConnectionState.Disconnected)
    {
        try
        {
            await connection.stop();
            console.log("SignalR 连接已手动停止。");
        } catch (err)
        {
            console.error("停止 SignalR 连接时出错:", err);
        } finally
        {
            if (connectionStore)
            {
                connectionStore.setSignalRConnectionStatus(false, false);
            }
            connection = null;
        }
    } else
    {
        console.log("SignalR 连接未建立或已关闭。");
        if (connectionStore)
        {
            connectionStore.setSignalRConnectionStatus(false, false);
        }
        connection = null;
    }
}

// --- 客户端 -> 服务器的方法调用 ---

/**
 * 确保连接存在且已连接
 */
function ensureConnected(): signalR.HubConnection
{
    if (!connection || connection.state !== signalR.HubConnectionState.Connected)
    {
        console.error("SignalR 连接未建立或已断开。");
        // 可以尝试自动重连或提示用户
        // getStatusStore()?.connectSignalR(); // 考虑是否要自动重连
        throw new Error("SignalR is not connected.");
    }
    return connection;
}

/**
 * 触发主工作流
 * @param request - 触发主工作流的请求数据
 */
async function triggerMainWorkflow(request: TriggerMainWorkflowRequestDto): Promise<void>
{
    try
    {
        await ensureConnected().invoke("TriggerMainWorkflow", request);
        console.log("SignalR: TriggerMainWorkflow 已发送", request);
    } catch (error)
    {
        console.error("调用 TriggerMainWorkflow 失败:", error);
        throw error; // 重新抛出错误，让调用者 (Store 或组件) 处理 UI 反馈
    }
}

/**
 * 触发微工作流
 * @param request - 触发微工作流的请求数据
 */
async function triggerMicroWorkflow(request: TriggerMicroWorkflowRequestDto): Promise<void>
{
    try
    {
        await ensureConnected().invoke("TriggerMicroWorkflow", request);
        console.log("SignalR: TriggerMicroWorkflow 已发送", request);
        // 注意：这里不直接更新状态，等待 ReceiveDisplayUpdate 通过 EventBus 分发
    } catch (error)
    {
        console.error("调用 TriggerMicroWorkflow 失败:", error);
        // 可以考虑通过 EventBus 发送一个错误事件给对应的 targetElementId
        const eventName = `microWorkflowUpdate:${request.targetElementId}` as const;
        eventBus.emit(eventName, {
            content: `请求失败: ${error instanceof Error ? error.message : error}`,
            status: StreamStatus.ERROR,
            updateMode: UpdateMode.FULL_SNAPSHOT // 出错时通常是替换
        });
        throw error; // 同时重新抛出错误
    }
}

/**
 * 重新生成 Block
 * @param request - 重新生成 Block 的请求数据
 */
async function regenerateBlock(request: RegenerateBlockRequestDto): Promise<void>
{
    try
    {
        await ensureConnected().invoke("RegenerateBlock", request);
        console.log("SignalR: RegenerateBlock 已发送", request);
    } catch (error)
    {
        console.error("调用 RegenerateBlock 失败:", error);
        throw error;
    }
}

/**
 * 解决冲突
 * @param request - 解决冲突的请求数据
 */
async function resolveConflict(request: ResolveConflictRequestDto): Promise<void>
{
    try
    {
        await ensureConnected().invoke("ResolveConflict", request);
        console.log("SignalR: ResolveConflict 已发送", request);
    } catch (error)
    {
        console.error("调用 ResolveConflict 失败:", error);
        throw error;
    }
}

// 导出服务方法
export const signalrService = {
    start: startConnection,
    stop: stopConnection,
    triggerMainWorkflow,
    triggerMicroWorkflow,
    regenerateBlock,
    resolveConflict,
    isConnected: () => connection?.state === signalR.HubConnectionState.Connected,
    getConnection: () => connection, // 可能需要暴露 connection 实例给高级用法？(谨慎使用)
};

// 添加一个辅助函数，用于在 signalrService 外部设置连接状态 (例如，在 App 初始化时)
// 这不是必需的，但有时可能有用
export function setSignalRStatusInStore(isConnected: boolean, isConnecting: boolean)
{
    try
    {
        getConnectionStore().setSignalRConnectionStatus(isConnected, isConnecting);
    } catch (error)
    {
        console.error("SignalR Service Helper: 更新状态时出错:", error);
    }
}