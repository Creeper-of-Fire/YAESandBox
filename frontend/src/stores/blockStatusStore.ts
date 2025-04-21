import {defineStore} from 'pinia';
import type {
    ConflictDetectedDto,
    BlockStatusUpdateDto,
    DisplayUpdateDto,
    BlockUpdateSignalDto,
} from '@/types/generated/api';
import {BlockStatusCode, BlockDataFields, StreamStatus, UpdateMode} from '@/types/generated/api'; // 引入 Enum
import {useTopologyStore} from './topologyStore';
import {useBlockContentStore} from './blockContentStore';
import {eventBus} from "@/services/eventBus.ts";

interface BlockStatusState {
    blockStatuses: Record<string, BlockStatusCode>; // Block ID -> 实时状态码
    activeConflict: ConflictDetectedDto | null;    // 当前待解决的冲突
    isLoadingAction: boolean;                   // 全局操作加载状态标志
    loadingActionMessage: string | null;       // （可选）加载时显示的消息
}

const defaultState: BlockStatusState = {
    blockStatuses: {},
    activeConflict: null,
    isLoadingAction: false,
    loadingActionMessage: null,
};

export const useBlockStatusStore = defineStore('blockStatus', {
    state: (): BlockStatusState => ({...defaultState}),

    getters: {
        /** 获取指定 ID 的 Block 实时状态码 */
        getBlockStatus(state): (id: string) => BlockStatusCode | undefined {
            return (id: string) => state.blockStatuses[id];
        },
        /** 获取当前激活的冲突信息 */
        getActiveConflict(state): ConflictDetectedDto | null {
            return state.activeConflict;
        },
        /** 获取全局操作加载状态 */
        getIsLoadingAction(state): boolean {
            return state.isLoadingAction;
        },
        /** 获取加载状态消息 */
        getLoadingActionMessage(state): string | null {
            return state.loadingActionMessage;
        },
        /** 检查指定 Block 是否处于加载状态 */
        isBlockLoading(): (id: string) => boolean {
            return (id: string) => this.blockStatuses[id] === BlockStatusCode.LOADING;
        },
        /** 检查指定 Block 是否处于冲突待解决状态 */
        isBlockResolvingConflict(): (id: string) => boolean {
            return (id: string) => this.blockStatuses[id] === BlockStatusCode.RESOLVING_CONFLICT;
        },
        /** 检查指定 Block 是否处于错误状态 */
        isBlockInError(): (id: string) => boolean {
            return (id: string) => this.blockStatuses[id] === BlockStatusCode.ERROR;
        },
    },

    actions: {
        /**
         * [SignalR Handler] 处理 Block 状态更新。
         */
        handleBlockStatusUpdate(data: BlockStatusUpdateDto) {
            if (!data.blockId || !data.statusCode) return;

            const blockId = data.blockId;
            const newStatusCode = data.statusCode;
            const oldStatusCode = this.blockStatuses[blockId];
            const blockContentStore = useBlockContentStore();
            const topologyStore = useTopologyStore();

            console.log(`BlockStatusStore: 收到 Block ${blockId} 状态更新: ${oldStatusCode} -> ${newStatusCode}`);

            // 统一更新状态码
            this.blockStatuses[blockId] = newStatusCode;

            // 清理旧冲突 (如果状态不再是 ResolvingConflict)
            if (newStatusCode !== BlockStatusCode.RESOLVING_CONFLICT && this.activeConflict?.blockId === blockId) {
                console.log(`BlockStatusStore: Block ${blockId} 状态不再是 ResolvingConflict，清除激活的冲突状态。`);
                this.activeConflict = null;
            }

            // 根据状态转换执行操作
            if (newStatusCode === BlockStatusCode.LOADING) {
                // 进入 Loading 状态
                // 检查 Block 内容是否已知，如果未知，则获取
                if (!blockContentStore.getBlockById(blockId)) {
                    console.log(`BlockStatusStore: Block ${blockId} 进入 Loading 状态，但内容未知，获取详情...`);
                    // 异步获取，不阻塞状态更新
                    blockContentStore.fetchBlockDetails(blockId);
                    // 注意：如果这是第一次出现 (新 Block)，还需要更新拓扑
                    // 这个逻辑判断比较复杂，依赖于是否能确定它是“新”的
                    // 暂时简化：假定新 Block 的信号会伴随拓扑更新信号（或等待临时方案触发）
                } else {
                    console.log(`BlockStatusStore: Block ${blockId} 进入 Loading 状态。`);
                }
            } else if (
                newStatusCode === BlockStatusCode.IDLE ||
                newStatusCode === BlockStatusCode.ERROR ||
                newStatusCode === BlockStatusCode.RESOLVING_CONFLICT
            ) {
                // 工作流完成（或进入冲突解决）
                console.log(`BlockStatusStore: Block ${blockId} 变为 ${newStatusCode}，获取最终详情...`);
                // 确保获取最终的 Block 内容和元数据
                blockContentStore.fetchBlockDetails(blockId, true); // 强制刷新
            } else if (newStatusCode === BlockStatusCode.DELETED) {
                // Block 被删除
                console.log(`BlockStatusStore: Block ${blockId} 状态变为 Deleted，清理状态并通知拓扑更新...`);
                delete this.blockStatuses[blockId];
                if (this.activeConflict?.blockId === blockId) {
                    this.activeConflict = null;
                }
                // 清理 ContentStore 缓存 (虽然 fetch 404 也会做，但这里更主动)
                delete blockContentStore.blocks[blockId];
                // 触发拓扑更新
                topologyStore.fetchAndUpdateTopology();
            } else if (newStatusCode === BlockStatusCode.NOT_FOUND) {
                // 后端明确告知未找到
                console.warn(`BlockStatusStore: Block ${blockId} 状态为 NotFound，清理状态。`);
                delete this.blockStatuses[blockId];
                delete blockContentStore.blocks[blockId];
                if (this.activeConflict?.blockId === blockId) {
                    this.activeConflict = null;
                }
                // 考虑是否需要更新拓扑
                topologyStore.fetchAndUpdateTopology();
            }
        },

        /**
         * [SignalR Handler] 处理显示内容更新 (主流程/重新生成)。
         * 现在这个方法只调用 BlockContentStore 来更新内容。
         */
        handleBlockDisplayUpdate(data: DisplayUpdateDto) {
            if (!data.contextBlockId || data.targetElementId) return; // 只处理无 targetElementId 的

            const blockId = data.contextBlockId;
            const content = data.content ?? null;
            const updateMode = data.updateMode ?? UpdateMode.FULL_SNAPSHOT; // 默认完全快照
            const streamStatus = data.streamingStatus ?? StreamStatus.COMPLETE; // 默认完成

            console.log(`BlockStatusStore: 处理 Block ${blockId} 内容更新 (模式: ${updateMode}, 状态: ${streamStatus})`);

            const blockContentStore = useBlockContentStore();

            // 检查 Block 详情是否已在缓存，如果不在，先异步获取
            if (!blockContentStore.getBlockById(blockId)) {
                console.warn(`BlockStatusStore: 收到 Block ${blockId} 内容更新，但其详情不在缓存中，先获取详情...`);
                // 异步获取，本次更新可能应用不上，后续更新会应用
                blockContentStore.fetchBlockDetails(blockId).then(() => {
                    // 获取成功后，尝试应用一次收到的内容（如果状态允许）
                    // 这里的逻辑比较微妙，可能需要更复杂的处理来确保不丢失更新
                    // 简单起见：依赖后续的 DisplayUpdate
                    console.log(`BlockStatusStore: Block ${blockId} 详情获取完成，等待后续内容更新。`);
                });
                // 返回，不直接应用本次更新
                return;
            }

            // 调用 BlockContentStore 更新内容
            blockContentStore.updateBlockContent(blockId, content, updateMode);

            // StreamStatus 现在仅供参考或调试，不直接改变 Block 状态
            if (streamStatus === StreamStatus.COMPLETE) {
                console.log(`BlockStatusStore: Block ${blockId} 内容流传输完成。`);
                // 等待 BlockStatusUpdate(Idle/Error/ResolvingConflict) 来确认最终状态
            } else if (streamStatus === StreamStatus.ERROR) {
                console.error(`BlockStatusStore: Block ${blockId} 内容流传输错误: ${content}`);
                // 等待 BlockStatusUpdate(Error) 来确认最终状态
            }
        },

        /**
         * [SignalR Handler] 处理检测到的冲突。
         */
        handleConflictDetected(data: ConflictDetectedDto) {
            if (!data.blockId) return;

            // 检查 Block 是否还存在 (可能在收到冲突前被删了)
            if (!this.blockStatuses[data.blockId] || this.blockStatuses[data.blockId] === BlockStatusCode.DELETED) {
                console.warn(`BlockStatusStore: 收到已删除或未知 Block ${data.blockId} 的冲突信息，忽略。`);
                return;
            }

            this.activeConflict = data;
            // 强制设置状态为 ResolvingConflict
            this.blockStatuses[data.blockId] = BlockStatusCode.RESOLVING_CONFLICT;
            console.warn(`BlockStatusStore: Block ${data.blockId} 检测到冲突，状态强制设为 ResolvingConflict。`);
        },

        /**
         * [SignalR Handler] 处理 Block 状态可能更新的信号。
         * 现在也负责发布 WorldState/GameState 变更事件。
         */
        handleBlockUpdateSignal(data: BlockUpdateSignalDto) {
            if (!data.blockId) return;

            const blockId = data.blockId;
            const changedFields = data.changedFields ?? [];
            console.log(`BlockStatusStore: 收到 Block ${blockId} 的更新信号`, changedFields);

            // 检查 Block 是否已知，如果未知则忽略信号 (或获取详情?)
            if (!this.blockStatuses[blockId] || this.blockStatuses[blockId] === BlockStatusCode.DELETED) {
                console.warn(`BlockStatusStore: 收到未知或已删除 Block ${blockId} 的更新信号，忽略。`);
                return;
            }

            const topologyStore = useTopologyStore();
            const blockContentStore = useBlockContentStore();

            // --- TODO: 临时拓扑更新逻辑 ---
            // 将来这部分逻辑会迁移到专门的 ReceiveTopologyUpdate 处理器
            const hasTopologyChange = changedFields.some(field =>
                field === BlockDataFields.PARENT_BLOCK_ID || field === BlockDataFields.CHILDREN_INFO
            );
            if (hasTopologyChange) {
                console.log(`BlockStatusStore: [TODO] 检测到拓扑相关信号 (ParentBlockId/ChildrenInfo)，触发拓扑更新。`);
                // 触发 TopologyStore 的刷新
                topologyStore.fetchAndUpdateTopology();
                // 注意：由于拓扑更新会重建图，通常会触发路径验证，可能不需要再单独获取该 block 详情
                // 但如果信号也包含其他字段，可能仍需获取详情
            }
            // --- END TODO ---

            // --- 发布 WorldState 变更事件 ---
            if (changedFields.includes(BlockDataFields.WORLD_STATE)) {
                const eventName = `${blockId}:WorldStateChanged` as const;
                const eventPayload = {changedEntityIds: data.changedEntityIds ?? null}; // 可选地传递受影响的实体 ID
                console.log(`BlockStatusStore: 发布事件 ${eventName}`, eventPayload);
                eventBus.emit(eventName, eventPayload); // <--- 发布事件
            }

            // --- 发布 GameState 变更事件 ---
            if (changedFields.includes(BlockDataFields.GAME_STATE)) {
                const eventName = `${blockId}:GameStateChanged` as const;
                // GameState 变更通常不携带详细信息，组件需要自行获取
                const eventPayload = {};
                console.log(`BlockStatusStore: 发布事件 ${eventName}`, eventPayload);
                eventBus.emit(eventName, eventPayload); // <--- 发布事件
            }

            // --- 处理其他字段变更（触发内容获取等） ---
            const requiresDetailFetch = changedFields.some(field =>
                field === BlockDataFields.BLOCK_CONTENT ||
                field === BlockDataFields.METADATA
            );
            
            // 目前如果有其他字段就触发全部的刷新

            // 如果需要获取详情 (且拓扑没触发全局刷新，或者就是需要最新的内容)
            if (requiresDetailFetch && !hasTopologyChange) {
                console.log(`BlockStatusStore: 信号指示内容/元数据等改变，获取 Block ${blockId} 详情...`);
                blockContentStore.fetchBlockDetails(blockId, true); // 强制刷新
            } else if (requiresDetailFetch && hasTopologyChange) {
                console.log(`BlockStatusStore: 拓扑已触发更新，但信号也包含其他字段，为保险起见，仍获取 Block ${blockId} 详情...`);
                // 在拓扑更新后可能需要再次获取详情，或者依赖拓扑更新后的路径验证逻辑
                // 简单起见，也获取一次
                blockContentStore.fetchBlockDetails(blockId, true);
            }
        },

        /**
         * 设置全局操作加载状态。
         * @param isLoading - 是否正在加载。
         * @param message - （可选）加载时显示的消息或标识符。
         */
        setLoadingAction(isLoading: boolean, message: string | null = null) {
            // 可以添加逻辑处理并发操作，例如使用 Set 存储 message
            this.isLoadingAction = isLoading;
            this.loadingActionMessage = isLoading ? message : null;
            if (this.isLoadingAction) console.log(`BlockStatusStore: 全局加载操作开始 [${message}]`);
            else console.log(`BlockStatusStore: 全局加载操作结束 [${message}]`);
        },

        /**
         * 清除所有状态 (用于加载新存档前)。
         */
        clearAllStatuses() {
            this.blockStatuses = {};
            this.activeConflict = null;
            this.isLoadingAction = false;
            this.loadingActionMessage = null;
            console.log("BlockStatusStore: 状态已清除。");
        }
    }
});