import {defineStore} from 'pinia';
import {BlocksService} from '@/types/generated/api';
import type {JsonBlockNode} from '@/types/generated/api';
import {useBlockContentStore} from './blockContentStore'; // Needed for checking node existence during rebuild

// 内部处理后的节点结构，包含父子引用
export interface ProcessedBlockNode {
    id: string;
    parent: ProcessedBlockNode | null;
    children: ProcessedBlockNode[];
    // 可以添加原始 JsonBlockNode 的引用或其他元信息，但保持简洁
}

interface TopologyState {
    rawTopology: JsonBlockNode | null;         // 从 API 获取的原始拓扑 JSON
    nodes: Map<string, ProcessedBlockNode>;    // ID -> ProcessedBlockNode 映射，方便快速查找
    rootNode: ProcessedBlockNode | null;       // 内存图中根节点的引用
    pathSelection: Record<string, string>;     // 父节点ID -> 选择的子节点ID
    currentPathLeafId: string | null;          // 当前路径最末端节点的 ID
    isLoading: boolean;
}

const defaultState: TopologyState = {
    rawTopology: null,
    nodes: new Map(),
    rootNode: null,
    pathSelection: {},
    currentPathLeafId: null,
    isLoading: false,
};

export const useTopologyStore = defineStore('topology', {
    state: (): TopologyState => ({...defaultState}),

    getters: {
        /** 获取根节点引用 */
        getRootNode(state): ProcessedBlockNode | null {
            return state.rootNode;
        },
        /** 根据 ID 获取处理后的节点引用 */
        getNodeById(state): (id: string) => ProcessedBlockNode | undefined {
            return (id: string) => state.nodes.get(id);
        },
        /** 获取当前路径叶节点的引用 */
        getCurrentPathLeafNode(): ProcessedBlockNode | null {
            if (!this.currentPathLeafId) return null;
            return this.nodes.get(this.currentPathLeafId) ?? null;
        },
        /** 获取当前路径上的所有节点引用 (从根到叶) */
        getCurrentPathNodes(): ProcessedBlockNode[] {
            const path: ProcessedBlockNode[] = [];
            let currentNode = this.getCurrentPathLeafNode;
            while (currentNode) {
                path.push(currentNode);
                currentNode = currentNode.parent;
            }
            return path.reverse(); // 从根到叶
        },
        /** 获取指定节点的所有子节点引用 */
        getChildrenNodesOf(): (nodeId: string) => ProcessedBlockNode[] {
            return (nodeId: string) => this.nodes.get(nodeId)?.children ?? [];
        },
        /** 获取指定节点的所有兄弟节点引用 (包括自身) */
        getSiblingNodesOf(): (nodeId: string) => ProcessedBlockNode[] {
            return (nodeId: string) => {
                const node = this.nodes.get(nodeId);
                return node?.parent?.children ?? (node ? [node] : []); // 如果有父节点，返回父节点的子节点列表，否则返回自身或空
            };
        },
        /** 获取指定父节点下，当前路径选择的子节点 ID */
        getSelectedChildIdOf(state): (parentId: string) => string | undefined {
            return (parentId: string) => state.pathSelection[parentId];
        },
        /** 获取指定父节点下，当前路径选择的子节点引用 */
        getSelectedChildNodeOf(): (parentId: string) => ProcessedBlockNode | undefined {
            return (parentId: string) => {
                const selectedId = this.pathSelection[parentId];
                return selectedId ? this.nodes.get(selectedId) : undefined;
            };
        },
    },

    actions: {
        /**
         * 从 API 获取最新拓扑，并重建内存图和路径状态。
         * 这是响应拓扑变更信号的核心方法。
         */
        async fetchAndUpdateTopology() {
            if (this.isLoading) return;
            this.isLoading = true;
            console.log("TopologyStore: 开始获取并更新拓扑...");
            try {
                const newRawTopology = await BlocksService.getApiBlocksTopology({});
                this.rawTopology = newRawTopology;

                // 重建内存图
                this._buildGraphFromTopology(newRawTopology);

                // 验证并设置路径
                this._validateAndSetPath();

                console.log("TopologyStore: 拓扑更新完成。根节点:", this.rootNode?.id, "叶节点:", this.currentPathLeafId);

            } catch (error) {
                console.error("TopologyStore: 获取或更新拓扑失败", error);
                // 可以考虑错误处理，比如清空拓扑或保留旧的？
                // this.rawTopology = null;
                // this.nodes.clear();
                // this.rootNode = null;
            } finally {
                this.isLoading = false;
            }
        },

        /**
         * [内部方法] 根据原始 JsonBlockNode 递归构建内存图 (ProcessedBlockNode)。
         * @param rawNode - 当前处理的原始 JSON 节点。
         * @param parentNode - 当前节点的父节点 (ProcessedBlockNode 引用)。
         * @returns 构建好的 ProcessedBlockNode。
         */
        _buildGraphFromTopology(rawNode: JsonBlockNode | null, parentNode: ProcessedBlockNode | null = null): ProcessedBlockNode | null {
            if (!rawNode) return null;

            // 清空旧图，准备重建
            this.nodes.clear();
            this.rootNode = null;
            console.log("TopologyStore: 开始构建内存图...");

            const recursiveBuild = (
                currentRaw: JsonBlockNode,
                currentParent: ProcessedBlockNode | null
            ): ProcessedBlockNode => {
                // 创建新节点
                const newNode: ProcessedBlockNode = {
                    id: currentRaw.id,
                    parent: currentParent,
                    children: [], // 先初始化为空
                };

                // 存入 Map
                this.nodes.set(newNode.id, newNode);

                // 处理子节点
                newNode.children = currentRaw.children
                    .map(rawChild => recursiveBuild(rawChild, newNode)) // 递归构建子节点
                    .filter(child => child !== null) as ProcessedBlockNode[]; // 过滤掉可能的 null (虽然理论上不应有)

                return newNode;
            };

            this.rootNode = recursiveBuild(rawNode, null);
            console.log(`TopologyStore: 内存图构建完成，共 ${this.nodes.size} 个节点。`);
            return this.rootNode; // 返回根节点，虽然主要目的是更新 state.nodes 和 state.rootNode
        },


        /**
         * [内部方法] 在拓扑重建后，验证当前路径选择和叶节点是否有效，
         * 如果无效或不存在，则设置默认路径。
         */
        _validateAndSetPath() {
            console.log("TopologyStore: 验证路径状态...");
            const blockContentStore = useBlockContentStore(); // 用于检查节点内容是否存在

            // 检查当前叶节点是否存在于新拓扑中
            if (this.currentPathLeafId && this.nodes.has(this.currentPathLeafId)) {
                console.log(`TopologyStore: 当前叶节点 ${this.currentPathLeafId} 在新拓扑中有效。尝试重建路径选择...`);
                // 叶节点有效，尝试基于它重建 pathSelection，确保路径连贯性
                this.rebuildPathSelectionForLeaf(this.currentPathLeafId);
                console.log(`TopologyStore: 重建后路径选择:`, this.pathSelection);
            } else {
                // 当前叶节点无效或不存在，需要设置默认路径
                if (this.currentPathLeafId) {
                    console.warn(`TopologyStore: 当前叶节点 ${this.currentPathLeafId} 在新拓扑中无效或不存在！`);
                }
                this.currentPathLeafId = null;
                this.pathSelection = {};
                this.setDefaultPathSelection();
            }
            // 可选：再次检查叶节点是否存在于 BlockContentStore (内容缓存)
            if (this.currentPathLeafId && !blockContentStore.getBlockById(this.currentPathLeafId)) {
                console.warn(`TopologyStore: 当前叶节点 ${this.currentPathLeafId} 在内容缓存中不存在，可能需要获取其内容。`);
                // 可以在这里触发获取叶节点内容
                // blockContentStore.fetchBlockDetails(this.currentPathLeafId);
            }
        },

        /**
         * 当用户在 Pager 中切换兄弟节点时调用。
         * @param newSiblingId - 要切换到的兄弟节点的 ID。
         */
        switchToSiblingNode(newSiblingId: string) {
            const targetNode = this.nodes.get(newSiblingId);
            if (!targetNode) {
                console.error(`TopologyStore: 尝试切换到未知的兄弟节点 ${newSiblingId}。`);
                // 拓扑可能不同步，触发一次更新
                this.fetchAndUpdateTopology();
                return;
            }
            const parentNode = targetNode.parent;
            if (!parentNode) {
                console.warn(`TopologyStore: 切换的节点 ${newSiblingId} 没有父节点，无法更新路径选择。将直接设置其为叶节点。`);
                this.currentPathLeafId = newSiblingId; // 直接设为叶子，但不改 pathSelection
                return;
            }

            console.log(`TopologyStore: 切换路径选择，父 ${parentNode.id} -> 子 ${newSiblingId}`);
            this.pathSelection[parentNode.id] = newSiblingId;

            // 从切换到的节点开始，递归更新下方的路径和叶节点
            this._updateLeafAndPathRecursively(targetNode);

            console.log("TopologyStore: 切换后路径选择", this.pathSelection);
            console.log("TopologyStore: 切换后当前叶节点", this.currentPathLeafId);
        },

        /**
         * [内部方法] 从给定节点开始，递归地找到最深（或按选择）的子孙，并更新路径选择和当前叶节点。
         * @param startNode - 开始递归的节点。
         */
        _updateLeafAndPathRecursively(startNode: ProcessedBlockNode) {
            let currentDeepestNode = startNode;

            while (currentDeepestNode.children.length > 0) {
                const parentId = currentDeepestNode.id;
                const children = currentDeepestNode.children;
                const currentSelection = this.pathSelection[parentId];
                let nextNode: ProcessedBlockNode | undefined;

                // 检查当前选择是否有效
                if (currentSelection) {
                    nextNode = children.find(child => child.id === currentSelection);
                }

                // 如果没有有效选择，或选择无效，则默认选第一个
                if (!nextNode) {
                    nextNode = children[0];
                    this.pathSelection[parentId] = nextNode.id; // 更新选择
                    console.log(`TopologyStore: (递归更新) 父 ${parentId} 默认/更新选择子 ${nextNode.id}`);
                }

                currentDeepestNode = nextNode;
            }
            // 更新最终的叶节点 ID
            this.currentPathLeafId = currentDeepestNode.id;
        },


        /**
         * 根据给定的叶节点 ID，向上回溯并重建 pathSelection 映射。
         */
        rebuildPathSelectionForLeaf(leafId: string) {
            const leafNode = this.nodes.get(leafId);
            if (!leafNode) {
                console.warn(`TopologyStore: 尝试为不存在的节点 ${leafId} 重建路径。中止。`);
                return;
            }

            console.log(`TopologyStore: 为叶节点 ${leafId} 重建路径选择...`);
            const newPathSelection: Record<string, string> = {};
            let currentNode: ProcessedBlockNode | null = leafNode;
            let childId: string | null = null;

            while (currentNode && currentNode.parent) { // 循环直到父节点为空 (根节点)
                let parentNode: ProcessedBlockNode;
                parentNode = currentNode.parent;
                newPathSelection[parentNode.id] = currentNode.id; // 父选了当前
                currentNode = parentNode;
            }

            this.pathSelection = newPathSelection;
            this.currentPathLeafId = leafId; // 确保叶节点ID也设置正确
        },

        /**
         * 设置默认的路径选择（通常是根节点的第一个子孙的最深处）。
         */
        setDefaultPathSelection() {
            console.log("TopologyStore: 尝试设置默认路径选择...");
            if (!this.rootNode || this.rootNode.children.length === 0) {
                console.log("TopologyStore: 无法设置默认路径，根节点或其子节点不存在。");
                this.currentPathLeafId = this.rootNode?.id ?? null; // 实在不行就把根节点设为叶节点
                this.pathSelection = {};
                return;
            }

            // 从根节点的第一个子节点开始递归查找
            const firstChild = this.rootNode.children[0];
            this.pathSelection = {}; // 清空旧选择
            this._updateLeafAndPathRecursively(firstChild); // 递归设置路径和叶节点
            // 确保根节点到第一个子节点的选择也被记录
            this.pathSelection[this.rootNode.id] = firstChild.id;
            console.log("TopologyStore: 默认路径设置完成，叶节点:", this.currentPathLeafId, "路径选择:", this.pathSelection);
        },

        // --- 持久化相关 ---
        /**
         * 清除状态 (用于加载新存档前)。
         */
        clearTopologyState() {
            this.rawTopology = null;
            this.nodes.clear();
            this.rootNode = null;
            this.pathSelection = {};
            this.currentPathLeafId = null;
            this.isLoading = false;
            console.log("TopologyStore: 状态已清除。");
        },

        /**
         * 恢复路径状态 (用于加载存档后)。
         * @param pathData - 包含 pathSelection 和 currentPathLeafId 的对象。
         */
        restorePathState(pathData: { pathSelection?: Record<string, string>, currentPathLeafId?: string | null }) {
            this.pathSelection = pathData.pathSelection ?? {};
            this.currentPathLeafId = pathData.currentPathLeafId ?? null;
            console.log("TopologyStore: 从存档恢复路径选择:", this.pathSelection);
            console.log("TopologyStore: 从存档恢复当前叶节点:", this.currentPathLeafId);
            // 恢复后，通常需要调用 _validateAndSetPath() 来确保与当前加载的拓扑一致
            this._validateAndSetPath();
        }
    }
});