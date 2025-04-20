// src/stores/narrativeStore.ts
import {defineStore} from 'pinia';
import {signalrService} from '../services/signalrService';
import {
    OpenAPI, // 用于获取 BASE URL
    BlocksService,
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
    BlockStatusCode, // 引入 Enum
    BlockDataFields, // 引入 Enum
    StreamStatus // 引入 Enum
} from '../types/generated/api.ts';
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

            // 使用缓存的 block 数据回溯父节点
            while (currentId) {
                let currentBlock: any;
                currentBlock = blockMap[currentId];
                // 如果在回溯过程中遇到未缓存的 block (理论上不应发生，除非状态不一致)，则停止
                if (!currentBlock) {
                    console.warn(`Store: 在构建路径时未找到 Block ${currentId} 的缓存数据，路径可能不完整。`);
                    // 可以考虑在此处触发一次 fetchBlocks 或 fetchBlockDetails
                    break;
                }
                path.push(currentId);
                currentId = currentBlock.parentBlockId ?? null;
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
            return node?.children?.map(child => child.id).filter(id => id !== null && id !== undefined) as string[] ?? [];
        },

        /**
         * 获取指定 ID 节点的所有兄弟节点 ID (包括自身)。
         */
        getSiblingIdsOf: (state) => (blockId: string): string[] => {
            const block = state.blocks[blockId];
            const parentId = block?.parentBlockId;
            if (!parentId || !state.topology) {
                // 如果是根节点或无父节点信息，或无拓扑，尝试直接返回自身
                return blockId ? [blockId] : [];
            }

            // 使用 getter 获取父节点的子节点
            // 注意：这里依赖 getChildrenIdsOf 能正确处理拓扑
            const parentChildren = useNarrativeStore().getChildrenIdsOf(parentId);
            return parentChildren;
        },

        /**
         * 检查指定 Block 是否处于加载状态 (Loading)。
         */
        isBlockLoading: (state) => (blockId: string): boolean => {
            const status = state.blockStatuses[blockId];
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
                // 初始化或更新实时状态 (如果尚未被 SignalR 更新覆盖)
                Object.keys(blocksData).forEach(id => {
                    if (!(id in this.blockStatuses)) { // 仅当状态不存在时才用 DTO 的初始化
                        const block = blocksData[id];
                        this.blockStatuses[id] = block?.statusCode ?? BlockStatusCode.IDLE;
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
                const topologyData = await BlocksService.getApiBlocksTopology({}); // 获取完整拓扑
                this.topology = topologyData;
                this.rootBlockId = topologyData.id ?? null;
                console.log("Store: Topology 加载完成", this.topology);

                // 初始化或验证当前路径和叶节点
                if (!this.currentPathLeafId && this.rootBlockId && this.topology?.children?.length) {
                    // 如果没有当前叶节点，设置一个默认值 (最深的第一个子孙)
                    this.setDefaultPathSelection();
                } else if (this.currentPathLeafId && !this.blocks[this.currentPathLeafId]) {
                    // 如果当前叶节点在新的拓扑/blocks数据中不存在了
                    console.warn(`Store: 当前叶节点 ${this.currentPathLeafId} 在新拓扑中不存在，重置路径。`);
                    this.pathSelection = {};
                    this.currentPathLeafId = null;
                    this.setDefaultPathSelection(); // 尝试设置新默认值
                } else if (this.currentPathLeafId) {
                    // 如果叶节点存在，重建路径确保一致性 (以防父子关系变化)
                    this.rebuildPathSelectionForLeaf(this.currentPathLeafId);
                }

            } catch (error) {
                console.error("Store: 获取 Topology 失败", error);
            } finally {
                this.isLoadingTopology = false;
            }
        },

        /**
         * 获取单个 Block 的详细信息。
         */
        async fetchBlockDetails(blockId: string) {
            try {
                const blockData = await BlocksService.getApiBlocks1({blockId});
                // 检查 Block 是否已被删除 (虽然理论上 fetch 应该404，但做个保险)
                if (blockData.statusCode === BlockStatusCode.DELETED || blockData.statusCode === BlockStatusCode.NOT_FOUND) {
                    console.warn(`Store: 获取 Block ${blockId} 详情时发现其状态为 ${blockData.statusCode}，将从缓存移除。`);
                    delete this.blocks[blockId];
                    delete this.blockStatuses[blockId];
                    // 可能需要触发拓扑更新
                    this.fetchTopology();
                    return; // 不再处理后续逻辑
                }

                this.blocks[blockId] = blockData;
                // 更新状态码缓存 (优先使用 SignalR 的实时状态，但如果 API 返回了不同状态，也可能需要更新？)
                // 简单起见，如果当前状态不是 Loading，则接受 API 返回的状态
                if (this.blockStatuses[blockId] !== BlockStatusCode.LOADING) {
                    this.blockStatuses[blockId] = blockData.statusCode ?? BlockStatusCode.IDLE;
                }
                console.log(`Store: Block ${blockId} 详情已更新/获取`);
            } catch (error) {
                console.error(`Store: 获取 Block ${blockId} 详情失败`, error);
                if ((error as any).status === 404) {
                    // 确认 Block 不存在，从缓存中移除
                    console.warn(`Store: 获取 Block ${blockId} 详情失败 (404)，从缓存移除。`);
                    delete this.blocks[blockId];
                    delete this.blockStatuses[blockId];
                    // Block 不存在了，需要更新拓扑
                    this.fetchTopology();
                }
                // 其他错误，例如网络问题，暂时不清除缓存
            }
        },

        // --- 处理 SignalR 推送 ---
        /**
         * 处理 Block 状态更新。
         * 这是核心逻辑，处理新 Block 创建、状态转换和删除。
         */
        handleBlockStatusUpdate(data: BlockStatusUpdateDto) {
            if (!data.blockId || !data.statusCode) return;

            const blockId = data.blockId;
            const newStatusCode = data.statusCode;
            const isKnownBlock = blockId in this.blocks; // 检查 block detail 是否已在缓存

            console.log(`Store: 收到 Block ${blockId} 状态更新: ${newStatusCode}. 已知: ${isKnownBlock}`);

            // 统一更新状态缓存
            this.blockStatuses[blockId] = newStatusCode;

            // --- 情况 A: 新 Block 首次出现 (通常是 Loading) ---
            if (!isKnownBlock && newStatusCode === BlockStatusCode.LOADING) {
                console.log(`Store: 检测到新 Block ${blockId} (Loading)，即将获取其详情和最新拓扑...`);
                // 立即获取新 Block 的详情 (主要是为了获取 parentId)
                this.fetchBlockDetails(blockId).then(() => {
                    // 获取详情后，更新拓扑结构以包含新节点
                    // 这里放在 .then 里确保我们有了 parentId 信息（虽然 fetchTopology 本身不直接用）
                    // 并且避免在 block 详情还没拿到时就更新拓扑导致潜在问题
                    this.fetchTopology();
                });
                // 理论上新 block 出现，不需要清除流式内容或冲突
            }
            // --- 情况 B: 已知 Block 完成工作流 (变为 Idle, Error, 或 ResolvingConflict) ---
            else if (isKnownBlock &&
                (newStatusCode === BlockStatusCode.IDLE ||
                    newStatusCode === BlockStatusCode.ERROR ||
                    newStatusCode === BlockStatusCode.RESOLVING_CONFLICT)) {
                console.log(`Store: 已知 Block ${blockId} 状态变为 ${newStatusCode}，清理流式内容并获取最终详情...`);
                // 1. 清理流式内容
                delete this.streamingContents[blockId];

                // 2. 清理旧冲突 (如果状态不再是 ResolvingConflict)
                if (newStatusCode !== BlockStatusCode.RESOLVING_CONFLICT && this.activeConflict?.blockId === blockId) {
                    console.log(`Store: Block ${blockId} 状态不再是 ResolvingConflict，清除激活的冲突状态。`);
                    this.activeConflict = null;
                }

                // 3. 获取最终的 Block 详情 (包括最终内容等)
                this.fetchBlockDetails(blockId);
            }
            // --- 情况 C: Block 被删除 ---
            else if (newStatusCode === BlockStatusCode.DELETED) {
                console.log(`Store: Block ${blockId} 状态变为 Deleted，从缓存移除并更新拓扑...`);
                // 1. 从所有相关缓存中移除
                delete this.blocks[blockId];
                delete this.blockStatuses[blockId];
                delete this.streamingContents[blockId];
                if (this.activeConflict?.blockId === blockId) {
                    this.activeConflict = null;
                }
                // 移除其他可能的缓存，如 entities, gameStates for this blockId

                // 2. 更新拓扑结构
                this.fetchTopology().then(() => {
                    // 3. 处理路径状态 (拓扑更新后执行)
                    // 检查当前路径是否包含被删除的节点
                    const currentPathIds = this.getCurrentPathBlockIds; // 获取更新拓扑后的路径
                    if (this.currentPathLeafId === blockId || !currentPathIds.includes(this.currentPathLeafId ?? '')) {
                        console.log(`Store: 当前叶节点 ${this.currentPathLeafId} 被删除或路径失效，尝试设置默认路径...`);
                        // 如果当前叶节点就是被删除的，或者删除导致当前叶节点已不在有效路径上
                        this.currentPathLeafId = null; // 清空
                        this.pathSelection = {};    // 清空
                        this.setDefaultPathSelection(); // 尝试设置新的默认路径
                    } else {
                        // 如果删除的是路径中间节点，需要重建选择
                        this.rebuildPathSelectionForLeaf(this.currentPathLeafId!);
                    }
                });
            }
            // --- 情况 D: 其他已知 Block 状态更新 (如 Idle -> Loading for Regenerate) ---
            else if (isKnownBlock) {
                console.log(`Store: 已知 Block ${blockId} 状态变为 ${newStatusCode} (非完成/删除状态)`);
                if (newStatusCode === BlockStatusCode.LOADING || newStatusCode === BlockStatusCode.RESOLVING_CONFLICT) {
                    // 进入这些状态时，清除可能残留的流式内容
                    delete this.streamingContents[blockId];
                }
                // 通常不需要立即获取详情，等待工作流完成或 DisplayUpdate
            }
            // --- 情况 E: 未知 Block 的非 Loading 状态 (理论上不应发生) ---
            else {
                console.warn(`Store: 收到未知 Block ${blockId} 的状态更新 (${newStatusCode})，但不是 Loading 状态。可能存在状态不同步。尝试获取详情和拓扑。`);
                // 异常情况处理：尝试获取信息以同步
                this.fetchBlockDetails(blockId);
                this.fetchTopology();
            }
        },

        /**
         * 处理显示内容更新。
         * **只负责更新内容，不负责设置 Block 的最终状态码。**
         */
        handleDisplayUpdate(data: DisplayUpdateDto) {
            if (!data.contextBlockId && !data.targetElementId) return;

            // --- 主工作流/重新生成更新 (更新 Block 内容) ---
            if (!data.targetElementId && data.contextBlockId) {
                const blockId = data.contextBlockId;
                const status = data.streamingStatus ?? StreamStatus.COMPLETE;

                // 检查关联的 Block 是否存在于缓存中
                const blockExists = blockId in this.blocks;

                switch (status) {
                    case StreamStatus.STREAMING:
                        // 无论 Block 是否已在缓存，都尝试更新流式内容
                        if (data.updateMode === 'Incremental') {
                            this.streamingContents[blockId] = (this.streamingContents[blockId] ?? "") + (data.content ?? "");
                        } else {
                            this.streamingContents[blockId] = data.content ?? "";
                        }
                        // 不再强制设置 Loading 状态，依赖 handleBlockStatusUpdate
                        break;
                    case StreamStatus.COMPLETE:
                        // 如果 Block 在缓存中，更新其最终内容
                        if (blockExists && data.content !== undefined) {
                            this.blocks[blockId].blockContent = data.content;
                            console.log(`Store: 更新 Block ${blockId} 的最终内容。`);
                        } else if (data.content !== undefined) {
                            // Block 不在缓存中，可能是状态同步问题或 DisplayUpdate 比 BlockDetails 先到
                            console.warn(`Store: 收到 Block ${blockId} 的最终内容，但 Block 不在缓存中，内容可能丢失。`);
                            // 可以在这里尝试触发一次 fetchBlockDetails
                            // this.fetchBlockDetails(blockId);
                        }
                        // 清除该 Block 的流式内容缓存
                        delete this.streamingContents[blockId];
                        // 不再根据 DisplayUpdate 设置 Idle 状态
                        break;
                    case StreamStatus.ERROR:
                        // 记录错误信息，但仅用于显示或调试
                        console.error(`Store: Block ${blockId} 内容生成流收到错误: ${data.content}`);
                        // 清除流式内容
                        delete this.streamingContents[blockId];
                        // 不再根据 DisplayUpdate 设置 Error 状态
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
            // 检查 blockId 是否有效 (可选)
            if (!(data.blockId in this.blockStatuses)) {
                console.warn(`Store: 收到未知 Block ${data.blockId} 的冲突信息，忽略。`);
                return;
            }
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

            // 检查 Block 是否已知，如果未知则忽略信号 (或者获取详情?)
            if (!(data.blockId in this.blocks)) {
                console.warn(`Store: 收到未知 Block ${data.blockId} 的更新信号，忽略。`);
                return;
            }

            // 简单策略：总是重新获取该 Block 的详细信息
            // 更精细策略：根据 changedFields 决定获取什么，以及是否需要更新拓扑
            const requiresTopologyUpdate = data.changedFields?.includes(BlockDataFields.CHILDREN_INFO) ||
                data.changedFields?.includes(BlockDataFields.PARENT_BLOCK_ID);

            if (requiresTopologyUpdate) {
                console.log(`Store: 更新信号指示拓扑可能变化，将获取 Block ${data.blockId} 详情并更新拓扑。`);
                // 先获取 Block 详情，再获取拓扑（或者并行？）
                this.fetchBlockDetails(data.blockId).then(() => {
                    this.fetchTopology();
                });
            } else {
                // 仅获取 Block 详情 (包含了 Content, Metadata, GameState 可能需要单独API)
                console.log(`Store: 更新信号未指示拓扑变化，仅获取 Block ${data.blockId} 详情。`);
                this.fetchBlockDetails(data.blockId);
            }

            // 如果是 WorldState 或 GameState 变化，可以进一步处理
            if (data.changedFields?.includes(BlockDataFields.WORLD_STATE)) {
                console.log(`Store: Block ${data.blockId} WorldState 可能已改变，考虑刷新实体或相关UI。`);
                // 可以在这里触发相关组件的刷新逻辑，或标记实体数据为“脏”
            }
            if (data.changedFields?.includes(BlockDataFields.GAME_STATE)) {
                console.log(`Store: Block ${data.blockId} GameState 可能已改变，考虑刷新相关UI。`);
                // 可以在这里触发相关组件的刷新逻辑，或标记 GameState 数据为“脏”
            }
        },

        // --- 触发后端操作 ---
        /**
         * 触发主工作流（创建新 Block）。
         */
        async triggerMainWorkflow(parentBlockId: string, workflowName: string, params: Record<string, any>) {
            if (this.isLoadingAction) return;

            // 检查父 Block 状态是否允许
            const parentStatus = this.blockStatuses[parentBlockId];
            if (parentStatus !== BlockStatusCode.IDLE) {
                console.warn(`Store: 父 Block ${parentBlockId} 状态为 ${parentStatus}，不允许触发主工作流`);
                // 可以选择抛出错误或通知用户
                alert(`父节点 (${parentBlockId}) 当前状态 (${parentStatus}) 不允许创建子节点。`);
                return;
            }
            // 检查父 Block 是否存在
            if (!(parentBlockId in this.blocks)) {
                console.error(`Store: 尝试在未知父 Block ${parentBlockId} 下创建子节点。`);
                alert(`父节点 (${parentBlockId}) 不存在。`);
                return;
            }

            this.isLoadingAction = true;
            const request: TriggerMainWorkflowRequestDto = {
                requestId: uuidv4(), // 生成唯一请求 ID
                parentBlockId,
                workflowName,
                params,
            };
            try {
                // 依赖后续的 ReceiveBlockStatusUpdate 来处理新 Block 的创建和状态更新
                await signalrService.triggerMainWorkflow(request);
                // 触发成功后，可以预期很快会收到新 Block 的 StatusUpdate (Loading)
            } catch (error) {
                console.error("Store: 触发主工作流失败", error);
                alert(`触发工作流失败: ${error instanceof Error ? error.message : error}`);
                this.isLoadingAction = false; // 出错时确保解除加载状态
            } finally {
                // 注意：isLoadingAction 应该在工作流完成或失败后才完全解除
                // 但 SignalR 的 invoke 是异步发送，不等待服务器完成
                // 暂时在这里解除，UI 主要依赖 Block 自身的状态来显示加载
                this.isLoadingAction = false;
            }
        },

        /**
         * 触发微工作流（更新 UI 元素）。
         */
        async triggerMicroWorkflow(contextBlockId: string, targetElementId: string, workflowName: string, params: Record<string, any>) {
            // 检查上下文 Block 是否存在
            if (!(contextBlockId in this.blocks)) {
                console.error(`Store: 尝试使用未知 Block ${contextBlockId} 作为微工作流上下文。`);
                alert(`上下文 Block (${contextBlockId}) 不存在。`);
                return;
            }

            const request: TriggerMicroWorkflowRequestDto = {
                requestId: uuidv4(),
                contextBlockId,
                targetElementId,
                workflowName,
                params,
            };
            try {
                // 清理之前的更新状态（可选，取决于UI需求）
                // delete this.microWorkflowUpdates[targetElementId];
                // 乐观设置加载状态
                this.microWorkflowUpdates[targetElementId] = {content: "处理中...", status: StreamStatus.STREAMING};

                await signalrService.triggerMicroWorkflow(request);
            } catch (error) {
                console.error("Store: 触发微工作流失败", error);
                this.microWorkflowUpdates[targetElementId] = {
                    content: `请求失败: ${error instanceof Error ? error.message : error}`,
                    status: StreamStatus.ERROR
                };
            }
        },

        /**
         * 重新生成现有 Block。
         */
        async regenerateBlock(blockId: string, workflowName: string, params: Record<string, any>) {
            if (this.isLoadingAction) return;

            const currentStatus = this.blockStatuses[blockId];
            // 检查 Block 是否存在
            if (!(blockId in this.blocks)) {
                console.error(`Store: 尝试重新生成未知 Block ${blockId}。`);
                alert(`Block (${blockId}) 不存在。`);
                return;
            }
            // 只允许在 Idle 或 Error 状态下重新生成
            if (currentStatus !== BlockStatusCode.IDLE && currentStatus !== BlockStatusCode.ERROR) {
                console.warn(`Store: Block ${blockId} 状态为 ${currentStatus}，不允许重新生成`);
                alert(`Block (${blockId}) 当前状态 (${currentStatus}) 不允许重新生成。`);
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
                // 不再乐观更新状态，等待服务器的 ReceiveBlockStatusUpdate(Loading)
                // delete this.streamingContents[blockId]; // 清理旧的流式内容 (可以在 StatusUpdate 时做)
                await signalrService.regenerateBlock(request);
            } catch (error) {
                console.error("Store: 重新生成 Block 失败", error);
                alert(`重新生成失败: ${error instanceof Error ? error.message : error}`);
                // 可以在这里手动设置回 Error 状态，或者依赖服务器后续更新
                // this.blockStatuses[blockId] = BlockStatusCode.ERROR;
                this.isLoadingAction = false;
            } finally {
                // 同 triggerMainWorkflow, 暂时在这里解除加载
                this.isLoadingAction = false;
            }
        },

        /**
         * 解决冲突。
         */
        async resolveConflict(requestId: string, blockId: string, resolvedCommands: AtomicOperationRequestDto[]) {
            if (this.isLoadingAction) return;

            const currentStatus = this.blockStatuses[blockId];
            // 检查 Block 是否存在
            if (!(blockId in this.blocks)) {
                console.error(`Store: 尝试解决未知 Block ${blockId} 的冲突。`);
                alert(`Block (${blockId}) 不存在。`);
                return;
            }
            if (currentStatus !== BlockStatusCode.RESOLVING_CONFLICT) {
                console.warn(`Store: Block ${blockId} 不处于 ResolvingConflict 状态`);
                alert(`Block (${blockId}) 未处于冲突状态。`);
                return;
            }
            // 检查 activeConflict 是否匹配
            if (!this.activeConflict || this.activeConflict.blockId !== blockId || this.activeConflict.requestId !== requestId) {
                console.warn(`Store: 当前激活的冲突与要解决的冲突不匹配。`);
                alert('要解决的冲突与当前激活的冲突不符。');
                return;
            }


            this.isLoadingAction = true;
            const request: ResolveConflictRequestDto = {
                requestId,
                blockId,
                resolvedCommands,
            };
            try {
                // 不再乐观地清除冲突状态，等待服务器的 StatusUpdate
                await signalrService.resolveConflict(request);
            } catch (error) {
                console.error("Store: 解决冲突失败", error);
                alert(`解决冲突失败: ${error instanceof Error ? error.message : error}`);
                // 错误处理：状态可能仍是 ResolvingConflict，或者服务器会发 Error
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

            // 检查 Block 是否存在
            if (!(blockId in this.blocks)) {
                console.error(`Store: 尝试向未知 Block ${blockId} 应用原子操作。`);
                alert(`Block (${blockId}) 不存在。`);
                return;
            }
            // 检查 Block 状态是否允许提交 (Idle 或 Loading 时允许，后者会暂存)
            const status = this.blockStatuses[blockId];
            if (status === BlockStatusCode.RESOLVING_CONFLICT) {
                console.error(`Store: Block ${blockId} 正在解决冲突，无法提交原子操作`);
                alert(`Block (${blockId}) 正在解决冲突，请先解决冲突。`);
                return;
            }
            if (status === BlockStatusCode.ERROR) {
                console.warn(`Store: Block ${blockId} 处于错误状态，提交原子操作可能无效`);
                // 可以选择允许或禁止
                alert(`Block (${blockId}) 处于错误状态，操作可能无效。`);
                // return; // 如果要禁止的话
            }
            if (status === BlockStatusCode.DELETED || status === BlockStatusCode.NOT_FOUND) {
                console.error(`Store: Block ${blockId} 已被删除或未找到，无法提交原子操作。`);
                alert(`Block (${blockId}) 已不存在。`);
                return;
            }

            // 可以设置一个短暂的加载状态？
            try {
                const request: BatchAtomicRequestDto = {operations};
                await AtomicService.postApiAtomic({blockId, requestBody: request});
                console.log(`Store: 原子操作已提交到 Block ${blockId}`);
                // 后端会发送 ReceiveBlockUpdateSignal，这里不需要手动获取数据
            } catch (error) {
                console.error(`Store: 提交原子操作到 Block ${blockId} 失败`, error);
                alert(`提交操作失败: ${error instanceof Error ? error.message : error}\n详细信息请查看控制台。`);
            }
        },

        // --- 路径和视图管理 ---
        /**
         * 当用户在 Pager 中切换兄弟节点时调用。
         * @param newSiblingId 要切换到的兄弟节点的 ID
         */
        switchToSibling(newSiblingId: string) {
            const newBlock = this.blocks[newSiblingId];
            // 检查目标兄弟节点是否存在
            if (!newBlock) {
                console.error(`Store: 尝试切换到未知兄弟节点 ${newSiblingId}。`);
                // 可能需要刷新 blocks 或 topology
                this.fetchBlocks();
                this.fetchTopology();
                return;
            }
            const parentId = newBlock?.parentBlockId;

            if (!parentId) {
                // 理论上非根节点都应该有父节点，如果 parentId 丢失，可能是数据问题
                console.warn(`Store: 切换的节点 ${newSiblingId} 没有父节点信息，无法更新路径选择`);
                // 尝试将当前叶节点设置为该节点，但不更新 pathSelection
                this.currentPathLeafId = newSiblingId;
                return;
            }

            console.log(`Store: 切换路径选择，父 ${parentId} -> 子 ${newSiblingId}`);
            // 更新路径选择记录
            this.pathSelection[parentId] = newSiblingId;

            // 重要：递归更新新路径下的叶节点和选择
            this.updateLeafAndPathRecursively(newSiblingId);

            console.log("Store: 更新后的路径选择", this.pathSelection);
            console.log("Store: 更新后的当前叶节点", this.currentPathLeafId);
        },

        /**
         * 递归地找到给定节点下的最深第一个子孙，并更新路径选择和当前叶节点。
         */
        updateLeafAndPathRecursively(startNodeId: string) {
            let deepestChildId = startNodeId;
            let currentChildren = this.getChildrenIdsOf(deepestChildId);

            while (currentChildren.length > 0) {
                // 检查 pathSelection 中是否已为当前节点选定子节点
                const selectedChild = this.pathSelection[deepestChildId];
                let nextNodeId: string;

                if (selectedChild && currentChildren.includes(selectedChild)) {
                    // 如果已选定且有效，则沿着已选路径继续
                    nextNodeId = selectedChild;
                } else {
                    // 如果未选定或选择无效，则默认选择第一个子节点
                    nextNodeId = currentChildren[0];
                    // 更新 pathSelection
                    this.pathSelection[deepestChildId] = nextNodeId;
                    console.log(`Store: (递归更新) 父 ${deepestChildId} 默认选择子 ${nextNodeId}`);
                }
                deepestChildId = nextNodeId;
                currentChildren = this.getChildrenIdsOf(deepestChildId);
            }
            // 更新最终的叶节点
            this.currentPathLeafId = deepestChildId;
        },


        /**
         * 根据给定的叶节点 ID，向上回溯并重建 pathSelection 映射。
         * 用于加载状态或切换到一个不在当前路径上的节点时。
         */
        rebuildPathSelectionForLeaf(leafId: string) {
            // 检查叶节点是否存在
            if (!this.blocks[leafId]) {
                console.warn(`Store: 尝试为不存在的叶节点 ${leafId} 重建路径，操作中止。`);
                return;
            }

            const newPathSelection: Record<string, string> = {};
            let currentId: string | null = leafId;
            let childId: string | null = null;

            while (currentId) {
                let block: UnwrapRef<BlockDetailDto>;
                block = this.blocks[currentId];
                // 如果在回溯中遇到未缓存的 block，停止（数据可能不完整）
                if (!block) {
                    console.warn(`Store: 重建路径时未找到 Block ${currentId} 的缓存，路径重建可能不完整。`);
                    break;
                }
                let parentId: string | null | undefined;
                parentId = block.parentBlockId;
                if (parentId && childId) {
                    newPathSelection[parentId] = childId; // 记录父节点选择了哪个子节点
                }
                // 如果到达根节点 __WORLD__ 或更高层（理论上不应发生），停止
                if (currentId === this.rootBlockId || !parentId) {
                    break;
                }
                childId = currentId;
                currentId = parentId;
            }
            this.pathSelection = newPathSelection;
            this.currentPathLeafId = leafId; // 确保当前叶节点也更新
            console.log(`Store: 为叶节点 ${leafId} 重建路径选择:`, this.pathSelection);
        },

        /**
         * 设置默认的路径选择（通常是根节点的第一个子孙的最深处）。
         * 在拓扑加载后且无有效叶节点时调用。
         */
        setDefaultPathSelection() {
            if (!this.rootBlockId || !this.topology || !this.topology.children || this.topology.children.length === 0) {
                console.log("Store: 无法设置默认路径，根节点或其子节点不存在。");
                this.currentPathLeafId = this.rootBlockId; // 实在不行就把根节点设为叶节点
                this.pathSelection = {};
                return;
            }

            console.log("Store: 尝试设置默认路径选择...");
            // 从根节点的第一个子节点开始递归查找
            const firstChildId = this.topology.children[0]?.id;
            if (firstChildId) {
                this.pathSelection = {}; // 清空旧选择
                this.updateLeafAndPathRecursively(firstChildId); // 递归设置路径和叶节点
                // 确保根节点到第一个子节点的选择也被记录
                this.pathSelection[this.rootBlockId] = firstChildId;
                console.log("Store: 默认路径设置完成，叶节点:", this.currentPathLeafId, "路径选择:", this.pathSelection);
            } else {
                console.warn("Store: 根节点的第一个子节点 ID 无效，无法设置默认路径。");
                this.currentPathLeafId = this.rootBlockId;
                this.pathSelection = {};
            }
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
                alert(`保存失败: ${error instanceof Error ? error.message : error}`);
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
                this.pathSelection = {}; // 先清空
                this.currentPathLeafId = null; // 先清空

                // 恢复盲存数据
                if (blindData) {
                    this.pathSelection = blindData.pathSelection ?? {};
                    this.currentPathLeafId = blindData.currentPathLeafId ?? null;
                    console.log("Store: 从盲存恢复路径选择:", this.pathSelection);
                    console.log("Store: 从盲存恢复当前叶节点:", this.currentPathLeafId);
                }

                // 加载成功后，需要重新获取服务器端的 Block 和拓扑数据
                // **重要：先获取 Blocks，再获取 Topology**
                // 因为 Topology 处理中可能需要 Blocks 数据来验证路径
                await this.fetchBlocks();
                await this.fetchTopology(); // fetchTopology 会处理路径的最终验证和设置

                // fetchTopology 会检查 currentPathLeafId 是否有效，并在需要时设置默认值或重建路径


            } catch (error) {
                console.error("Store: 加载状态失败", error);
                alert(`加载失败: ${error instanceof Error ? error.message : error}`);
                // 加载失败，可能需要恢复到加载前的状态或清空？
                // 简单处理：保持清空后的状态，让用户重新开始或再次尝试加载
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
                alert("不允许删除根节点！");
                return;
            }
            // 检查 Block 是否存在
            if (!(blockId in this.blocks)) {
                console.error(`Store: 尝试删除未知 Block ${blockId}。`);
                alert(`Block (${blockId}) 不存在。`);
                return;
            }
            // 可以在这里添加确认对话框 (已在 Bubble 中添加)

            this.isLoadingAction = true;
            try {
                await BlockManagementService.deleteApiManageBlocks({blockId, recursive, force});
                console.log(`Store: 删除 Block ${blockId} 的请求已发送`);
                // 后端会发送 BlockStatusUpdate(Deleted) 和 BlockUpdateSignal(父节点的ChildrenInfo改变)
                // Store 会通过 handleBlockStatusUpdate 处理状态和拓扑更新
            } catch (error) {
                console.error(`Store: 删除 Block ${blockId} 失败`, error);
                alert(`删除失败: ${error instanceof Error ? error.message : error}`);
            } finally {
                this.isLoadingAction = false;
            }
        }

    }
});