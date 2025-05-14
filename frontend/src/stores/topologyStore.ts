import {defineStore} from 'pinia';
import {BlocksService, type BlockTopologyNodeDto} from '@/types/generated/api';
import {useBlockContentStore} from './blockContentStore'; // Needed for checking node existence during rebuild

// 内部处理后的节点结构，包含父子引用
export interface ProcessedBlockNode {
    id: string;
    parent: ProcessedBlockNode | null;
    children: ProcessedBlockNode[];
    // 可以添加原始 JsonBlockNode 的引用或其他元信息，但保持简洁
}

interface TopologyState {
    rawTopology: BlockTopologyNodeDto[] | null;         // 从 API 获取的原始拓扑 JSON
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

// 定义根节点 ID 常量 (与后端一致)
const WORLD_ROOT_ID = "__WORLD__";

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
            console.log(`TopologyStore: 调用节点生成，长度：${path.length}`);
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
                this._updateLeafAndPathRecursivelyFromRoot();

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
         * [内部方法] 根据扁平的 BlockTopologyNodeDto 列表构建内存图 (ProcessedBlockNode)。
         * @param flatList - 从 API 获取的扁平节点列表。
         */
        _buildGraphFromTopology(flatList: BlockTopologyNodeDto[] | null) {
            // 清空旧图，准备重建
            this.nodes.clear();
            this.rootNode = null;

            if (!flatList || flatList.length === 0) {
                console.warn("TopologyStore: 接收到的扁平拓扑列表为空，无法构建图。");
                return; // 如果列表为空，直接返回
            }

            console.log("TopologyStore: 开始从扁平列表构建内存图...");

            // 第一次遍历：创建所有节点实例并放入 Map
            for (const dto of flatList) {
                // 基本验证
                if (!dto.blockId) {
                    console.warn("TopologyStore: 发现缺少 blockId 的节点数据，已跳过:", dto);
                    continue;
                }
                const newNode: ProcessedBlockNode = {
                    id: dto.blockId,
                    parent: null, // 稍后设置
                    children: [],  // 稍后设置
                };
                if (!this.nodes.has(newNode.id)) { // 防止重复 ID (理论上不应发生)
                    this.nodes.set(newNode.id, newNode);
                } else {
                    console.warn(`TopologyStore: 发现重复的 Block ID '${newNode.id}'，已忽略。`);
                }
            }
            console.log(`TopologyStore: 第一遍完成，创建了 ${this.nodes.size} 个节点实例。`);


            // 第二次遍历：连接父子关系
            let foundRoot: ProcessedBlockNode | null = null;
            for (const node of this.nodes.values()) { // 遍历 Map 中的节点
                // 从原始 DTO 中找到对应的数据来获取 parentId (或者在第一次遍历时存起来)
                const dto = flatList.find(d => d.blockId === node.id); // 效率稍低，但简单
                // 优化：可以在第一次遍历时创建一个 Map<string, string | null> 来存储 parentId

                if (!dto) {
                    console.error(`TopologyStore: 逻辑错误，无法在原始列表中找到节点 ${node.id} 的 DTO。`);
                    continue;
                }

                const parentId = dto.parentBlockId;

                if (parentId) {
                    const parentNode = this.nodes.get(parentId);
                    if (parentNode) {
                        node.parent = parentNode;
                        // 确保不重复添加子节点 (虽然理论上第二次遍历不会重复)
                        if (!parentNode.children.some(child => child.id === node.id)) {
                            parentNode.children.push(node);
                        }
                    } else {
                        // 父节点 ID 存在，但在 Map 中找不到 -> 数据不一致
                        console.warn(`TopologyStore: 节点 ${node.id} 的父节点 ID '${parentId}' 在节点列表中未找到，可能数据不一致或父节点在子树范围外。`);
                        // 这种节点视为孤儿节点或次级根节点 (如果允许的话)
                    }
                } else {
                    // parentId 为 null 或 undefined，这应该是根节点
                    if (node.id === WORLD_ROOT_ID) { // 确认是全局根节点
                        foundRoot = node;
                    } else {
                        // 如果允许非 __WORLD__ 的根节点（例如获取子树时）
                        // 或者是有父ID但父节点丢失的情况
                        if (!foundRoot) foundRoot = node; // 如果还没找到根，将第一个无父节点设为根
                        console.warn(`TopologyStore: 节点 ${node.id} 没有父节点，被视为根节点或孤儿节点。`);
                    }
                }
            }

            // 设置根节点
            if (foundRoot) {
                this.rootNode = foundRoot;
                console.log(`TopologyStore: 内存图构建完成，根节点已设置为 ${this.rootNode.id}。`);
            } else if (this.nodes.size > 0) {
                console.error("TopologyStore: 内存图构建完成，但未找到有效的根节点！");
                // 尝试找第一个节点作为根？或者报错？
                this.rootNode = this.nodes.values().next().value ?? null;
                if (this.rootNode) console.warn(`TopologyStore: 回退：将第一个节点 ${this.rootNode.id} 设置为根节点。`);
            } else {
                console.log("TopologyStore: 内存图构建完成，没有节点被创建。");
            }
        },


        /**
         * [内部方法] 在刷新页面/载入后，验证当前路径选择和叶节点是否有效，
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
                this._updateLeafAndPathRecursivelyFromRoot();
            }
            // // 可选：再次检查叶节点是否存在于 BlockContentStore (内容缓存)
            // if (this.currentPathLeafId && !blockContentStore.getBlockById(this.currentPathLeafId)) {
            //     console.warn(`TopologyStore: 当前叶节点 ${this.currentPathLeafId} 在内容缓存中不存在，可能需要获取其内容。`);
            //     // 可以在这里触发获取叶节点内容
            //     // blockContentStore.fetchBlockDetails(this.currentPathLeafId);
            // }
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

                // 如果没有有效选择，或选择无效，则默认选最后一个
                if (!nextNode) {
                    nextNode = children[children.length - 1];
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
         * 从根节点开始，递归设置路径和叶节点。
         */
        _updateLeafAndPathRecursivelyFromRoot() {
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
            // TODO 之后应该存储所有叶节点，以及当前选择路径的叶节点，用于全面恢复数据
        }
    }
});