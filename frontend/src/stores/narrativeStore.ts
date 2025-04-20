// src/stores/narrativeStore.ts
import {defineStore} from 'pinia';
import {signalrService} from '@/services/signalrService';
import {
    OpenAPI, // 用于获取 BASE URL
    BlocksService,
    EntitiesService,
    GameStateService,
    AtomicService,
    PersistenceService,
    BlockManagementService, // 如果需要前端删除功能
    // 引入所有需要的 DTO 类型
    type BlockDetailDto,
    type JsonBlockNode,
    type ConflictDetectedDto,
    type BlockStatusUpdateDto,
    type DisplayUpdateDto,
    type BlockUpdateSignalDto,
    type TriggerMainWorkflowRequestDto,
    type TriggerMicroWorkflowRequestDto,
    type RegenerateBlockRequestDto,
    type ResolveConflictRequestDto,
    type BatchAtomicRequestDto,
    type AtomicOperationRequestDto,
    type UpdateGameStateRequestDto,
    type EntitySummaryDto,
    type EntityDetailDto,
    type GameStateDto,
    BlockStatusCode, // 引入 Enum
    BlockDataFields, // 引入 Enum
    StreamStatus, EntityType // 引入 Enum
} from '@/types/generated/api.ts';
import {v4 as uuidv4} from 'uuid';
import type {UnwrapRef} from "vue"; // 用于生成 requestId

// --- 辅助类型 ---
interface NarrativeState {
    // SignalR 连接状态
    isSignalRConnected: boolean;
    isSignalRConnecting: boolean;

    // 核心数据
    blocks: Record<string, BlockDetailDto>; // 所有 Block 的详细信息缓存 (ID -> Detail)
    topology: JsonBlockNode | null;        // Block 树的拓扑结构
    rootBlockId: string | null;            // 根节点 ID (通常是 __WORLD__)

    // 实时状态 (来自 SignalR)
    blockStatuses: Record<string, BlockStatusCode>; // Block 的实时状态码 (ID -> StatusCode)
    // 用于存储流式内容的临时状态 (ID -> streaming content string)
    streamingContents: Record<string, string>;
    // 微工作流更新目标 (TargetElementId -> content/status) - 简化处理，只存最新内容
    microWorkflowUpdates: Record<string, { content: string | null, status: StreamStatus }>;

    // 冲突管理
    activeConflict: ConflictDetectedDto | null; // 当前待解决的冲突信息

    // 当前用户视图/路径状态
    // `pathSelection` 决定了从根到叶子的路径: Map<父节点ID, 选择的子节点ID>
    pathSelection: Record<string, string>;
    // `currentPathLeafId` 标识当前用户界面滚动到的最下方（或焦点）Block 的 ID
    currentPathLeafId: string | null;

    // 按需加载的数据缓存 (可选，根据应用复杂度决定是否需要)
    // entities: Record<string, Record<string, EntityDetailDto>>; // BlockID -> EntityID -> EntityDetail
    // gameStates: Record<string, GameStateDto>; // BlockID -> GameState

    // 加载状态
    isLoadingTopology: boolean;
    isLoadingBlocks: boolean;
    isLoadingAction: boolean; // 通用操作加载状态
}

// --- 默认状态 ---
const defaultState: NarrativeState = {
    isSignalRConnected: false,
    isSignalRConnecting: false,
    blocks: {},
    topology: null,
    rootBlockId: null, // 需要在加载拓扑后设置
    blockStatuses: {},
    streamingContents: {},
    microWorkflowUpdates: {},
    activeConflict: null,
    pathSelection: {},
    currentPathLeafId: null, // 需要从盲存或默认逻辑初始化
    isLoadingTopology: false,
    isLoadingBlocks: false,
    isLoadingAction: false,
};

export const useNarrativeStore = defineStore('narrative', {
    state: (): NarrativeState => ({...defaultState}),

    getters: {
        /**
         * 获取指定 ID 的 Block 详细信息。
         */
        getBlockById: (state) => (blockId: string): BlockDetailDto | undefined => {
            return state.blocks[blockId];
        },

        /**
         * 获取指定 ID 的 Block 实时状态码。
         */
        getBlockStatus: (state) => (blockId: string): BlockStatusCode | undefined => {
            return state.blockStatuses[blockId];
        },

        /**
         * 获取指定 Block 的流式内容（如果有）。
         */
        getStreamingContent: (state) => (blockId: string): string | undefined => {
            return state.streamingContents[blockId];
        },

        /**
         * 获取指定 TargetElementId 的微工作流更新。
         */
        getMicroWorkflowUpdate: (state) => (targetElementId: string): { content: string | null, status: StreamStatus } | undefined => {
            return state.microWorkflowUpdates[targetElementId];
        },

        /**
         * 获取当前激活的冲突信息。
         */
        getActiveConflict: (state): ConflictDetectedDto | null => {
            return state.activeConflict;
        },

        /**
         * 获取当前选择的路径上的所有 Block ID 列表（从根到叶）。
         * 这是核心的视图逻辑。
         */
        getCurrentPathBlockIds(state): string[] {
            if (!state.topology || !state.currentPathLeafId) {
                return [];
            }

            const path: string[] = [];
            let currentId: string | null = state.currentPathLeafId;
            const blockMap = state.blocks; // 方便查找父节点

            while (currentId) {
                path.push(currentId);
                let block: BlockDetailDto;
                block = blockMap[currentId];
                currentId = block?.parentBlockId ?? null; // 使用缓存的 block 数据回溯
            }

            return path.reverse(); // 从根到叶
        },

        /**
         * 获取指定父节点下，当前路径选择的子节点 ID。
         */
        getSelectedChildOf: (state) => (parentId: string): string | undefined => {
            return state.pathSelection[parentId];
        },

        /**
         * 获取指定 ID 节点的所有子节点 ID。
         */
        getChildrenIdsOf: (state) => (blockId: string): string[] => {
            if (!state.topology) return [];

            // 递归查找函数
            function findNode(node: JsonBlockNode, id: string): JsonBlockNode | null {
                if (node.id === id) return node;
                if (node.children) {
                    for (const child of node.children) {
                        const found = findNode(child, id);
                        if (found) return found;
                    }
                }
                return null;
            }

            const node = findNode(state.topology, blockId);
            return node?.children?.map(child => child.id).filter(id => id !== null) as string[] ?? [];
        },

        /**
         * 获取指定 ID 节点的所有兄弟节点 ID (包括自身)。
         */
        getSiblingIdsOf: (state) => (blockId: string): string[] => {
            const block = state.blocks[blockId];
            const parentId = block?.parentBlockId;
            if (!parentId || !state.topology) return blockId ? [blockId] : []; // 如果没有父节点或拓扑，返回自身

            const parentChildren = useNarrativeStore().getChildrenIdsOf(parentId); // 使用 getter 获取父节点的子节点
            return parentChildren;
        },

        /**
         * 检查指定 Block 是否处于加载状态 (Loading 或 Regenerating)。
         */
        isBlockLoading: (state) => (blockId: string): boolean => {
            const status = state.blockStatuses[blockId];
            // 可以根据需要添加更多状态
            return status === BlockStatusCode.LOADING;
        },

        /**
         * 检查指定 Block 是否处于冲突待解决状态。
         */
        isBlockResolvingConflict: (state) => (blockId: string): boolean => {
            return state.blockStatuses[blockId] === BlockStatusCode.RESOLVING_CONFLICT;
        },

        /**
         * 检查指定 Block 是否处于错误状态。
         */
        isBlockInError: (state) => (blockId: string): boolean => {
            return state.blockStatuses[blockId] === BlockStatusCode.ERROR;
        }

    },

    actions: {
        // --- SignalR 连接管理 ---
        /**
         * 连接到 SignalR Hub。
         */
        async connectSignalR() {
            if (this.isSignalRConnected || this.isSignalRConnecting) return;
            try {
                await signalrService.start(OpenAPI.BASE);
            } catch (error) {
                console.error("Store: SignalR 连接失败", error);
                // 可以在 UI 上显示错误信息
            }
        },

        /**
         * 断开 SignalR 连接。
         */
        async disconnectSignalR() {
            await signalrService.stop();
        },

        /**
         * 由 signalrService 调用，更新连接状态。
         */
        setSignalRConnectionStatus(isConnected: boolean, isConnecting: boolean) {
            this.isSignalRConnected = isConnected;
            this.isSignalRConnecting = isConnecting;
        },

        // --- 数据获取 (REST API) ---
        /**
         * 获取所有 Block 的摘要信息。
         */
        async fetchBlocks() {
            if (this.isLoadingBlocks) return;
            this.isLoadingBlocks = true;
            try {
                const blocksData = await BlocksService.getApiBlocks();
                this.blocks = blocksData;
                // 初始化或更新实时状态
                Object.keys(blocksData).forEach(id => {
                    if (!this.blockStatuses[id]) {
                        this.blockStatuses[id] = blocksData[id].statusCode ?? BlockStatusCode.IDLE; // 使用 DTO 中的状态作为初始状态
                    }
                });
                console.log("Store: Blocks 加载完成");
            } catch (error) {
                console.error("Store: 获取 Blocks 失败", error);
            } finally {
                this.isLoadingBlocks = false;
            }
        },

        /**
         * 获取 Block 拓扑结构。
         */
        async fetchTopology() {
            if (this.isLoadingTopology) return;
            this.isLoadingTopology = true;
            try {
                // 假设我们总是获取完整的树
                // TODO 获取不完整的树
                const topologyData = await BlocksService.getApiBlocksTopology({}); // 传递空对象或不传参数获取完整拓扑
                this.topology = topologyData;
                this.rootBlockId = topologyData.id ?? null;
                console.log("Store: Topology 加载完成", this.topology);
                // 如果没有当前叶节点，尝试设置一个默认值 (例如根的第一个子节点)
                if (!this.currentPathLeafId && this.rootBlockId && this.topology?.children?.length) {
                    // 需要递归找到最深的第一个子孙
                    let defaultLeaf = this.topology.children[0];
                    while (defaultLeaf.children && defaultLeaf.children.length > 0) {
                        defaultLeaf = defaultLeaf.children[0];
                    }
                    if (defaultLeaf.id) {
                        this.currentPathLeafId = defaultLeaf.id;
                        // 同时需要构建默认的 pathSelection
                        this.rebuildPathSelectionForLeaf(defaultLeaf.id);
                    }

                }
            } catch (error) {
                console.error("Store: 获取 Topology 失败", error);
            } finally {
                this.isLoadingTopology = false;
            }
        },

        /**
         * 获取单个 Block 的详细信息 (例如在 UpdateSignal 后)。
         */
        async fetchBlockDetails(blockId: string) {
            try {
                const blockData = await BlocksService.getApiBlocks1({blockId});
                this.blocks[blockId] = blockData;
                // 更新状态码缓存，但优先使用实时状态
                if (!this.blockStatuses[blockId] || this.blockStatuses[blockId] !== BlockStatusCode.LOADING) {
                    this.blockStatuses[blockId] = blockData.statusCode ?? BlockStatusCode.IDLE;
                }
                console.log(`Store: Block ${blockId} 详情已更新`);
            } catch (error) {
                console.error(`Store: 获取 Block ${blockId} 详情失败`, error);
                if ((error as any).status === 404) {
                    // Block 可能已被删除
                    delete this.blocks[blockId];
                    delete this.blockStatuses[blockId];
                    // 可能需要更新拓扑或路径
                }
            }
        },

        /**
         * 获取指定 Block 的实体列表 (示例，按需加载)
         */
        async fetchEntitiesForBlock(blockId: string): Promise<EntitySummaryDto[] | null> {
            try {
                const entities = await EntitiesService.getApiEntities({blockId});
                // 可以选择缓存这些实体摘要，或直接返回给调用者
                return entities;
            } catch (error) {
                console.error(`Store: 获取 Block ${blockId} 的实体列表失败`, error);
                return null;
            }
        },

        /**
         * 获取指定实体的详细信息 (示例，按需加载)
         */
        async fetchEntityDetails(blockId: string, entityType: EntityType, entityId: string): Promise<EntityDetailDto | null> {
            try {
                const entityDetail = await EntitiesService.getApiEntities1({blockId, entityType, entityId});
                // 可以缓存或直接返回
                return entityDetail;
            } catch (error) {
                console.error(`Store: 获取实体 ${entityType}/${entityId} (Block ${blockId}) 详情失败`, error);
                return null;
            }
        },

        /**
         * 获取指定 Block 的 GameState (示例，按需加载)
         */
        async fetchGameStateForBlock(blockId: string): Promise<GameStateDto | null> {
            try {
                const gameState = await GameStateService.getApiBlocksGameState({blockId});
                // 可以缓存或直接返回
                return gameState;
            } catch (error) {
                console.error(`Store: 获取 Block ${blockId} 的 GameState 失败`, error);
                return null;
            }
        },


        // --- 处理 SignalR 推送 ---
        /**
         * 处理 Block 状态更新。
         */
        handleBlockStatusUpdate(data: BlockStatusUpdateDto) {
            if (!data.blockId || !data.statusCode) return;
            console.log(`Store: 更新 Block ${data.blockId} 状态为 ${data.statusCode}`);
            this.blockStatuses[data.blockId] = data.statusCode;

            // 如果是从 Loading 变为 Idle 或 Error，清除流式内容
            if (data.statusCode === BlockStatusCode.IDLE || data.statusCode === BlockStatusCode.ERROR) {
                delete this.streamingContents[data.blockId];
                // 可选：状态稳定后自动获取最新详情
                if (this.blocks[data.blockId]) { // 只在已知 block 时获取
                    this.fetchBlockDetails(data.blockId);
                }
            }
            // 如果状态变为 ResolvingConflict，确保清除旧的冲突（理论上不应该发生）
            if (data.statusCode !== BlockStatusCode.RESOLVING_CONFLICT && this.activeConflict?.blockId === data.blockId) {
                this.activeConflict = null;
            }
            // 如果 Block 被删除
            if (data.statusCode === BlockStatusCode.DELETED) {
                delete this.blocks[data.blockId];
                delete this.blockStatuses[data.blockId];
                delete this.streamingContents[data.blockId];
                // TODO: 需要更新拓扑结构，或者让前端根据 getChildrenIdsOf 返回空来处理
                this.fetchTopology(); // 简单粗暴地重新获取拓扑
            }
        },

        /**
         * 处理显示内容更新。
         */
        handleDisplayUpdate(data: DisplayUpdateDto) {
            if (!data.contextBlockId && !data.targetElementId) return;

            // --- 主工作流/重新生成更新 (更新 Block 内容) ---
            if (!data.targetElementId && data.contextBlockId) {
                const blockId = data.contextBlockId;
                const status = data.streamingStatus ?? StreamStatus.COMPLETE; // 默认为 Complete

                switch (status) {
                    case StreamStatus.STREAMING:
                        // 追加或替换流式内容
                        if (data.updateMode === 'Incremental') { // 假设 'Incremental' 意味着追加
                            this.streamingContents[blockId] = (this.streamingContents[blockId] ?? "") + (data.content ?? "");
                        } else { // FullSnapshot 或未指定
                            this.streamingContents[blockId] = data.content ?? "";
                        }
                        // 确保状态为 Loading
                        if (this.blockStatuses[blockId] !== BlockStatusCode.LOADING) {
                            this.blockStatuses[blockId] = BlockStatusCode.LOADING;
                        }
                        break;
                    case StreamStatus.COMPLETE:
                        // 更新最终内容，清除流式内容
                        if (this.blocks[blockId] && data.content !== undefined) {
                            this.blocks[blockId].blockContent = data.content; // 更新缓存的 Block 内容
                        } else if (data.content !== undefined) {
                            // 如果 Block 不在缓存中，可能需要创建一个临时的或获取它
                            // 简单处理：如果block不存在，也尝试更新一下status
                            console.warn(`Store: 收到 Block ${blockId} 的最终内容，但 Block 不在缓存中`);
                        }
                        delete this.streamingContents[blockId];
                        // 状态应由 BlockStatusUpdate 更新，但这里可以做个保险
                        if (this.blockStatuses[blockId] === BlockStatusCode.LOADING) {
                            this.blockStatuses[blockId] = BlockStatusCode.IDLE; // 假定成功完成对应 Idle
                        }
                        break;
                    case StreamStatus.ERROR:
                        // 清除流式内容，标记错误
                        delete this.streamingContents[blockId];
                        console.error(`Store: Block ${blockId} 内容生成出错: ${data.content}`);
                        // 状态应由 BlockStatusUpdate 更新，但这里可以做个保险
                        if (this.blockStatuses[blockId] === BlockStatusCode.LOADING) {
                            this.blockStatuses[blockId] = BlockStatusCode.ERROR;
                        }
                        break;
                }
            }
            // --- 微工作流更新 (更新特定 UI 元素) ---
            else if (data.targetElementId) {
                const targetId = data.targetElementId;
                const status = data.streamingStatus ?? StreamStatus.COMPLETE;
                const currentUpdate = this.microWorkflowUpdates[targetId] ?? {content: null, status: StreamStatus.COMPLETE};

                switch (status) {
                    case StreamStatus.STREAMING:
                        if (data.updateMode === 'Incremental') {
                            currentUpdate.content = (currentUpdate.content ?? "") + (data.content ?? "");
                        } else {
                            currentUpdate.content = data.content ?? "";
                        }
                        currentUpdate.status = StreamStatus.STREAMING;
                        break;
                    case StreamStatus.COMPLETE:
                        currentUpdate.content = data.content ?? "";
                        currentUpdate.status = StreamStatus.COMPLETE;
                        break;
                    case StreamStatus.ERROR:
                        console.error(`Store: 微工作流目标 ${targetId} 出错: ${data.content}`);
                        currentUpdate.content = data.content ?? "发生错误"; // 显示错误信息
                        currentUpdate.status = StreamStatus.ERROR;
                        break;
                }
                this.microWorkflowUpdates[targetId] = {...currentUpdate}; // 确保响应性
            }
        },

        /**
         * 处理检测到的冲突。
         */
        handleConflictDetected(data: ConflictDetectedDto) {
            if (!data.blockId) return;
            this.activeConflict = data;
            // 状态应由 BlockStatusUpdate 更新为 ResolvingConflict，这里可以再确认一下
            this.blockStatuses[data.blockId] = BlockStatusCode.RESOLVING_CONFLICT;
            delete this.streamingContents[data.blockId]; // 清除可能存在的流式内容
            console.warn(`Store: Block ${data.blockId} 检测到冲突，等待解决`);
        },

        /**
         * 处理 Block 状态可能更新的信号。
         */
        handleBlockUpdateSignal(data: BlockUpdateSignalDto) {
            if (!data.blockId) return;
            console.log(`Store: 收到 Block ${data.blockId} 的更新信号`, data.changedFields);

            // 简单策略：如果 Block 在当前路径上，或者就是当前叶节点，则重新获取其详细信息
            // 更精细策略：根据 changedFields 决定获取什么
            const currentPathIds = this.getCurrentPathBlockIds; // 使用 getter 获取当前路径
            const needsRefresh = currentPathIds.includes(data.blockId) || data.blockId === this.currentPathLeafId;

            if (needsRefresh) {
                // 如果是拓扑变化，优先刷新拓扑
                if (data.changedFields?.includes(BlockDataFields.CHILDREN_INFO) || data.changedFields?.includes(BlockDataFields.PARENT_BLOCK_ID)) {
                    console.log(`Store: 检测到拓扑变化，重新获取拓扑和 Block ${data.blockId} 详情`);
                    this.fetchTopology(); // 获取最新拓扑
                }
                // 总是获取 Block 详情（包含了内容、元数据等）
                this.fetchBlockDetails(data.blockId);
                // 如果是 WorldState 或 GameState 变化，可以考虑清除相关缓存或触发特定UI更新
                if (data.changedFields?.includes(BlockDataFields.WORLD_STATE)) {
                    console.log(`Store: Block ${data.blockId} WorldState 可能已改变，考虑刷新实体或相关UI`);
                    // 例如，如果实体编辑器打开着，可以提示刷新
                }
                if (data.changedFields?.includes(BlockDataFields.GAME_STATE)) {
                    console.log(`Store: Block ${data.blockId} GameState 可能已改变，考虑刷新相关UI`);
                }

            } else {
                console.log(`Store: Block ${data.blockId} 不在当前路径，暂时忽略更新信号`);
                // 或者标记为“可能过时”，当用户导航到它时再加载
            }
        },

        // --- 触发后端操作 ---
        /**
         * 触发主工作流（创建新 Block）。
         */
        async triggerMainWorkflow(parentBlockId: string, workflowName: string, params: Record<string, any>) {
            if (this.isLoadingAction) return;
            this.isLoadingAction = true;
            // 检查父 Block 状态是否允许
            const parentStatus = this.blockStatuses[parentBlockId];
            if (parentStatus !== BlockStatusCode.IDLE) {
                console.warn(`Store: 父 Block ${parentBlockId} 状态为 ${parentStatus}，不允许触发主工作流`);
                this.isLoadingAction = false;
                return; // 或者抛出错误
            }

            const request: TriggerMainWorkflowRequestDto = {
                requestId: uuidv4(), // 生成唯一请求 ID
                parentBlockId,
                workflowName,
                params,
            };
            try {
                // 可以在这里乐观地创建一个临时的 Loading 子 Block 状态，但这比较复杂
                // 依赖 ReceiveBlockStatusUpdate 来处理新 Block 的创建和状态更新
                await signalrService.triggerMainWorkflow(request);
            } catch (error) {
                console.error("Store: 触发主工作流失败", error);
                // 处理错误，例如通知用户
            } finally {
                this.isLoadingAction = false;
            }
        },

        /**
         * 触发微工作流（更新 UI 元素）。
         */
        async triggerMicroWorkflow(contextBlockId: string, targetElementId: string, workflowName: string, params: Record<string, any>) {
            // 微工作流通常不阻塞主要操作，不设置 isLoadingAction
            const request: TriggerMicroWorkflowRequestDto = {
                requestId: uuidv4(),
                contextBlockId,
                targetElementId,
                workflowName,
                params,
            };
            try {
                // 清理之前的更新状态（可选）
                // delete this.microWorkflowUpdates[targetElementId];
                // 乐观设置加载状态？
                this.microWorkflowUpdates[targetElementId] = {content: "加载中...", status: StreamStatus.STREAMING};

                await signalrService.triggerMicroWorkflow(request);
            } catch (error) {
                console.error("Store: 触发微工作流失败", error);
                this.microWorkflowUpdates[targetElementId] = {content: "请求失败", status: StreamStatus.ERROR};
            }
        },

        /**
         * 重新生成现有 Block。
         */
        async regenerateBlock(blockId: string, workflowName: string, params: Record<string, any>) {
            if (this.isLoadingAction) return;
            const currentStatus = this.blockStatuses[blockId];
            if (currentStatus !== BlockStatusCode.IDLE && currentStatus !== BlockStatusCode.ERROR) {
                console.warn(`Store: Block ${blockId} 状态为 ${currentStatus}，不允许重新生成`);
                return;
            }

            this.isLoadingAction = true;
            const request: RegenerateBlockRequestDto = {
                requestId: uuidv4(),
                blockId,
                workflowName,
                params,
            };
            try {
                // 乐观更新状态为 Loading
                this.blockStatuses[blockId] = BlockStatusCode.LOADING;
                delete this.streamingContents[blockId]; // 清除旧的流式内容

                await signalrService.regenerateBlock(request);
            } catch (error) {
                console.error("Store: 重新生成 Block 失败", error);
                this.blockStatuses[blockId] = BlockStatusCode.ERROR; // 失败则标记为错误
                this.isLoadingAction = false; // 确保解除加载状态
            } finally {
                // 注意：isLoadingAction 在工作流完成前可能不应该解除，但这取决于 SignalR 的反馈
                // 暂时在这里解除，依赖后续 StatusUpdate
                this.isLoadingAction = false;
            }
        },

        /**
         * 解决冲突。
         */
        async resolveConflict(requestId: string, blockId: string, resolvedCommands: AtomicOperationRequestDto[]) {
            if (this.isLoadingAction) return;
            const currentStatus = this.blockStatuses[blockId];
            if (currentStatus !== BlockStatusCode.RESOLVING_CONFLICT) {
                console.warn(`Store: Block ${blockId} 不处于 ResolvingConflict 状态`);
                return;
            }

            this.isLoadingAction = true;
            const request: ResolveConflictRequestDto = {
                requestId,
                blockId,
                resolvedCommands,
            };
            try {
                // 乐观地清除冲突状态
                this.activeConflict = null;
                // 状态将由服务器通过 ReceiveBlockStatusUpdate 更新

                await signalrService.resolveConflict(request);
            } catch (error) {
                console.error("Store: 解决冲突失败", error);
                // 错误处理：可能需要恢复 activeConflict 状态？或者让用户重试
                // 状态可能仍是 ResolvingConflict，或者服务器会发 Error
                this.isLoadingAction = false;
            } finally {
                // 同上，暂时在这里解除加载
                this.isLoadingAction = false;
            }
        },

        /**
         * 提交原子操作 (例如，编辑实体后)。
         */
        async applyAtomicOperations(blockId: string, operations: AtomicOperationRequestDto[]) {
            if (!operations || operations.length === 0) return;
            // 可能需要检查 Block 状态是否允许提交 (Idle 或 Loading)
            const status = this.blockStatuses[blockId];
            if (status === BlockStatusCode.RESOLVING_CONFLICT) {
                console.error(`Store: Block ${blockId} 正在解决冲突，无法提交原子操作`);
                return; // 或抛出错误
            }
            if (status === BlockStatusCode.ERROR) {
                console.warn(`Store: Block ${blockId} 处于错误状态，提交原子操作可能无效`);
            }


            // 可以设置一个短暂的加载状态？
            try {
                const request: BatchAtomicRequestDto = {operations};
                await AtomicService.postApiAtomic({blockId, requestBody: request});
                console.log(`Store: 原子操作已提交到 Block ${blockId}`);
                // 注意：后端会发送 ReceiveBlockUpdateSignal，这里不需要手动获取数据
                // 如果是 Loading 状态，操作会被暂存，AI完成后会处理冲突
            } catch (error) {
                console.error(`Store: 提交原子操作到 Block ${blockId} 失败`, error);
                // 通知用户错误
            }
        },

        /**
         * 更新 GameState (示例)。
         */
        async updateGameState(blockId: string, settingsToUpdate: Record<string, any>) {
            try {
                const request: UpdateGameStateRequestDto = {settingsToUpdate};
                await GameStateService.patchApiBlocksGameState({blockId, requestBody: request});
                console.log(`Store: GameState for Block ${blockId} 已更新`);
                // 后端会发送 ReceiveBlockUpdateSignal (带 GameState 字段)
                // this.fetchGameStateForBlock(blockId); // 可以选择立即刷新缓存
            } catch (error) {
                console.error(`Store: 更新 Block ${blockId} GameState 失败`, error);
            }
        },

        // --- 路径和视图管理 ---
        /**
         * 当用户在 Pager 中切换兄弟节点时调用。
         * @param newSiblingId 要切换到的兄弟节点的 ID
         */
        switchToSibling(newSiblingId: string) {
            const newBlock = this.blocks[newSiblingId];
            const parentId = newBlock?.parentBlockId;

            if (!parentId) {
                console.warn("Store: 切换的节点没有父节点，无法更新路径选择");
                return;
            }

            console.log(`Store: 切换路径选择，父 ${parentId} -> 子 ${newSiblingId}`);
            // 更新路径选择记录
            this.pathSelection[parentId] = newSiblingId;

            // 更新当前视图的叶节点
            this.currentPathLeafId = newSiblingId;

            // 重要：如果切换的节点不是叶子节点，需要递归更新其下方的路径选择
            // 例如，默认选择新路径下的第一个子孙作为新的叶节点
            let deepestChildId = newSiblingId;
            let currentChildren = this.getChildrenIdsOf(deepestChildId);
            while (currentChildren.length > 0) {
                const firstChildId = currentChildren[0];
                this.pathSelection[deepestChildId] = firstChildId; // 选择第一个子节点
                deepestChildId = firstChildId;
                currentChildren = this.getChildrenIdsOf(deepestChildId);
            }
            this.currentPathLeafId = deepestChildId; // 更新叶节点为新路径的最深处

            console.log("Store: 更新后的路径选择", this.pathSelection);
            console.log("Store: 更新后的当前叶节点", this.currentPathLeafId);
        },

        /**
         * 根据给定的叶节点 ID，向上回溯并重建 pathSelection 映射。
         * 用于加载状态或切换到一个不在当前路径上的节点时。
         */
        rebuildPathSelectionForLeaf(leafId: string) {
            const newPathSelection: Record<string, string> = {};
            let currentId: string | null = leafId;
            let childId: string | null = null;

            while (currentId) {
                let block: UnwrapRef<BlockDetailDto>;
                block = this.blocks[currentId];
                let parentId: string | null | undefined;
                parentId = block?.parentBlockId;
                if (parentId && childId) {
                    newPathSelection[parentId] = childId; // 记录父节点选择了哪个子节点
                }
                childId = currentId;
                currentId = parentId ?? null;
            }
            this.pathSelection = newPathSelection;
            this.currentPathLeafId = leafId; // 确保当前叶节点也更新
            console.log(`Store: 为叶节点 ${leafId} 重建路径选择:`, this.pathSelection);
        },

        // --- 持久化 ---
        /**
         * 保存当前状态到后端。
         */
        async saveState() {
            if (this.isLoadingAction) return;
            this.isLoadingAction = true;
            try {
                // 准备盲存数据
                const blindData = {
                    // 保存每个分支路径的最底层叶节点 ID (这比较复杂，需要遍历拓扑?)
                    // 简化：只保存当前路径的叶节点 ID 和路径选择
                    currentPathLeafId: this.currentPathLeafId,
                    pathSelection: this.pathSelection,
                    // 你可以添加任何其他需要前端恢复的状态
                };

                const blob = await PersistenceService.postApiPersistenceSave({requestBody: blindData});

                // 创建下载链接
                const url = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.style.display = 'none';
                a.href = url;
                // 时间戳文件名
                const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
                a.download = `YAESandBox_Save_${timestamp}.json`;
                document.body.appendChild(a);
                a.click();
                window.URL.revokeObjectURL(url);
                document.body.removeChild(a);

                console.log("Store: 状态已保存");

            } catch (error) {
                console.error("Store: 保存状态失败", error);
            } finally {
                this.isLoadingAction = false;
            }
        },

        /**
         * 从文件加载状态。
         */
        async loadState(archiveFile: File) {
            if (this.isLoadingAction) return;
            this.isLoadingAction = true;
            try {
                const formData = {archiveFile};
                const blindData = await PersistenceService.postApiPersistenceLoad({formData});

                console.log("Store: 状态加载成功，收到盲存数据:", blindData);

                // 清理当前状态 (除了连接状态)
                this.blocks = {};
                this.topology = null;
                this.rootBlockId = null;
                this.blockStatuses = {};
                this.streamingContents = {};
                this.microWorkflowUpdates = {};
                this.activeConflict = null;
                // pathSelection 和 currentPathLeafId 会从 blindData 恢复

                // 恢复盲存数据
                if (blindData) {
                    this.pathSelection = blindData.pathSelection ?? {};
                    this.currentPathLeafId = blindData.currentPathLeafId ?? null;
                } else {
                    this.pathSelection = {};
                    this.currentPathLeafId = null;
                }


                // 加载成功后，需要重新获取服务器端的 Block 和拓扑数据
                await this.fetchBlocks();
                await this.fetchTopology();

                // 如果加载后 currentPathLeafId 仍然是 null，fetchTopology 会尝试设置默认值
                // 如果 blindData 中的 currentPathLeafId 对应的 block 不存在了（可能存档损坏或版本不兼容），也需要处理
                if (this.currentPathLeafId && !this.blocks[this.currentPathLeafId]) {
                    console.warn(`Store: 加载的叶节点 ${this.currentPathLeafId} 不存在，重置路径`);
                    this.currentPathLeafId = null; // 重置
                    this.pathSelection = {};
                    // fetchTopology 会尝试设置默认
                } else if (this.currentPathLeafId) {
                    // 确保加载的路径选择与实际拓扑一致
                    this.rebuildPathSelectionForLeaf(this.currentPathLeafId);
                }


            } catch (error) {
                console.error("Store: 加载状态失败", error);
                // 通知用户加载失败
            } finally {
                this.isLoadingAction = false;
            }
        },

        // --- (可选) 手动删除 Block ---
        /**
         * 删除指定的 Block。
         * @param blockId 要删除的 Block ID。
         * @param recursive 是否递归删除子节点 (强烈建议为 true)。
         * @param force 是否无视状态强制删除 (谨慎使用)。
         */
        async deleteBlock(blockId: string, recursive: boolean = true, force: boolean = false) {
            // 检查是否是根节点
            if (blockId === this.rootBlockId) {
                console.error("Store: 不允许删除根节点");
                return;
            }
            // 可以在这里添加确认对话框

            this.isLoadingAction = true;
            try {
                await BlockManagementService.deleteApiManageBlocks({blockId, recursive, force});
                console.log(`Store: 删除 Block ${blockId} 的请求已发送`);
                // 后端会发送 BlockStatusUpdate(Deleted) 和 BlockUpdateSignal(父节点的ChildrenInfo改变)
                // store 会通过 handleBlockStatusUpdate 和 handleBlockUpdateSignal 处理状态和拓扑更新
            } catch (error) {
                console.error(`Store: 删除 Block ${blockId} 失败`, error);
                // 通知用户错误
            } finally {
                this.isLoadingAction = false;
            }
        }

    }
});

// 可以在 Store 初始化时自动连接 SignalR (如果需要)
// const storeInstance = useNarrativeStore();
// if (!storeInstance.isSignalRConnected && !import.meta.env.SSR) { // 避免 SSR 环境下连接
//     storeInstance.connectSignalR();
// }