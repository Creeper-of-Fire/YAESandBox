// src/services/signalrService.ts
import * as signalR from "@microsoft/signalr";
import { useNarrativeStore } from '@/stores/narrativeStore'; // 引入 Pinia Store
import type {
    BlockStatusUpdateDto,
    BlockUpdateSignalDto,
    ConflictDetectedDto,
    DisplayUpdateDto,
    RegenerateBlockRequestDto,
    ResolveConflictRequestDto,
    TriggerMainWorkflowRequestDto,
    TriggerMicroWorkflowRequestDto
} from "@/types/generated/api.ts"; // 引入需要的 DTO 类型

// SignalR Hub 的相对路径
const HUB_URL = "/gamehub";

let connection: signalR.HubConnection | null = null;
let store: ReturnType<typeof useNarrativeStore> | null = null;

// 获取 Pinia Store 实例的函数 (延迟初始化)
function getStore() {
    if (!store) {
        store = useNarrativeStore();
    }
    return store;
}

/**
 * 启动 SignalR 连接
 * @param baseUrl 服务器的基础 URL (例如 http://localhost:5000)
 */
async function startConnection(baseUrl: string): Promise<void> {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        console.log("SignalR 连接已存在。");
        return;
    }

    const hubUrl = `${baseUrl}${HUB_URL}`;
    console.log(`尝试连接到 SignalR Hub: ${hubUrl}`);

    // 如果已有连接但未连接，先停止
    if (connection) {
        try {
            await connection.stop();
        } catch (err) {
            console.error("停止旧 SignalR 连接时出错:", err);
        }
    }

    connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
            // skipNegotiation: true, // 如果需要，可以配置选项
            // transport: signalR.HttpTransportType.WebSockets // 强制使用 WebSockets
        })
        .withAutomaticReconnect({
            nextRetryDelayInMilliseconds: retryContext => {
                // 设置重连间隔，例如 0, 2, 10, 30 秒... 最大 60 秒
                const delay = Math.min(Math.pow(2, retryContext.previousRetryCount) * 1000, 60000);
                console.log(`SignalR 连接丢失，将在 ${delay / 1000} 秒后尝试重连 (${retryContext.previousRetryCount + 1} 次尝试)...`);
                return delay;
            }
        })
        .configureLogging(signalR.LogLevel.Information) // 配置日志级别
        .build();

    // --- 注册服务器 -> 客户端的方法处理器 ---
    // 这些处理器会调用 Pinia Store 的 actions 来更新状态

    connection.on("ReceiveBlockStatusUpdate", (data: BlockStatusUpdateDto) => {
        console.log("SignalR: 收到 BlockStatusUpdate", data);
        getStore().handleBlockStatusUpdate(data);
    });

    connection.on("ReceiveDisplayUpdate", (data: DisplayUpdateDto) => {
        console.log("SignalR: 收到 DisplayUpdate", data);
        getStore().handleDisplayUpdate(data);
    });

    connection.on("ReceiveConflictDetected", (data: ConflictDetectedDto) => {
        console.warn("SignalR: 收到 ConflictDetected", data);
        getStore().handleConflictDetected(data);
    });

    connection.on("ReceiveBlockUpdateSignal", (data: BlockUpdateSignalDto) => {
        console.log("SignalR: 收到 BlockUpdateSignal", data);
        getStore().handleBlockUpdateSignal(data);
    });

    // --- 处理连接状态变化 ---

    connection.onreconnecting(error => {
        console.warn(`SignalR 正在重连... 原因: ${error}`);
        getStore().setSignalRConnectionStatus(false, true); // isConnected: false, isConnecting: true
    });

    connection.onreconnected(connectionId => {
        console.log(`SignalR 重连成功！Connection ID: ${connectionId}`);
        getStore().setSignalRConnectionStatus(true, false); // isConnected: true, isConnecting: false
    });

    connection.onclose(error => {
        console.error(`SignalR 连接已关闭。原因: ${error}`);
        getStore().setSignalRConnectionStatus(false, false); // isConnected: false, isConnecting: false
        // 可以在这里尝试手动重启连接，或者让 withAutomaticReconnect 处理
    });

    // --- 启动连接 ---
    try {
        getStore().setSignalRConnectionStatus(false, true); // isConnected: false, isConnecting: true
        await connection.start();
        console.log("SignalR 连接成功！Connection ID:", connection.connectionId);
        getStore().setSignalRConnectionStatus(true, false); // isConnected: true, isConnecting: false
    } catch (err) {
        console.error("SignalR 连接失败:", err);
        getStore().setSignalRConnectionStatus(false, false); // isConnected: false, isConnecting: false
        connection = null; // 清理连接对象
        // 可以抛出错误或进行其他处理
        throw new Error(`无法连接到 SignalR Hub: ${err}`);
    }
}

/**
 * 停止 SignalR 连接
 */
async function stopConnection(): Promise<void> {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        try {
            await connection.stop();
            console.log("SignalR 连接已手动停止。");
        } catch (err) {
            console.error("停止 SignalR 连接时出错:", err);
        } finally {
            getStore().setSignalRConnectionStatus(false, false);
            connection = null; // 确保清理
        }
    } else {
        console.log("SignalR 连接未建立或已关闭。");
        getStore().setSignalRConnectionStatus(false, false);
        connection = null;
    }
}

// --- 客户端 -> 服务器的方法调用 ---

/**
 * 确保连接存在且已连接
 */
function ensureConnected(): signalR.HubConnection {
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
        console.error("SignalR 连接未建立或已断开。");
        throw new Error("SignalR is not connected.");
    }
    return connection;
}

/**
 * 触发主工作流
 * @param request - 触发主工作流的请求数据
 */
async function triggerMainWorkflow(request: TriggerMainWorkflowRequestDto): Promise<void> {
    try {
        await ensureConnected().invoke("TriggerMainWorkflow", request);
        console.log("SignalR: TriggerMainWorkflow 已发送", request);
    } catch (error) {
        console.error("调用 TriggerMainWorkflow 失败:", error);
        // 可以在这里通知用户或进行重试
        throw error; // 重新抛出错误，让调用者处理
    }
}

/**
 * 触发微工作流
 * @param request - 触发微工作流的请求数据
 */
async function triggerMicroWorkflow(request: TriggerMicroWorkflowRequestDto): Promise<void> {
    try {
        await ensureConnected().invoke("TriggerMicroWorkflow", request);
        console.log("SignalR: TriggerMicroWorkflow 已发送", request);
    } catch (error) {
        console.error("调用 TriggerMicroWorkflow 失败:", error);
        throw error;
    }
}

/**
 * 重新生成 Block
 * @param request - 重新生成 Block 的请求数据
 */
async function regenerateBlock(request: RegenerateBlockRequestDto): Promise<void> {
    try {
        await ensureConnected().invoke("RegenerateBlock", request);
        console.log("SignalR: RegenerateBlock 已发送", request);
    } catch (error) {
        console.error("调用 RegenerateBlock 失败:", error);
        throw error;
    }
}

/**
 * 解决冲突
 * @param request - 解决冲突的请求数据
 */
async function resolveConflict(request: ResolveConflictRequestDto): Promise<void> {
    try {
        await ensureConnected().invoke("ResolveConflict", request);
        console.log("SignalR: ResolveConflict 已发送", request);
    } catch (error) {
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
    // 可能需要一个获取当前连接状态的方法
    isConnected: () => connection?.state === signalR.HubConnectionState.Connected,
};