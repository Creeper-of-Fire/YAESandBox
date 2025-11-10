import {computed, type Ref} from 'vue';
import type {Connection, Edge, Node,EdgeChange, NodeDragEvent} from '@vue-flow/core';
import type {WorkflowEditorContext} from '#/components/workflow/editor/WorkflowEditorContext.ts';
import type {TuumConnectionEndpoint, WorkflowConnection} from '#/types/generated/workflow-config-api-client';
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import {WORKFLOW_INPUT_SOURCE_ID} from "#/utils/constants.ts";
import type {TuumConfig} from "@yaesandbox-frontend/core-services/types";

// --- 自定义类型定义 ---

type CustomNodeType = 'workflow-inputs' | 'tuum';

interface InputNodeData
{
    inputs: string[];
}

interface TuumNodeData
{
    tuum: TuumConfig;
}

type CustomNodeData = InputNodeData | TuumNodeData;

type CustomNode = Node<CustomNodeData, any, CustomNodeType>;

type CustomEdge = Edge;

// --- ID 生成工具函数 ---
const INPUTS_NODE_ID = 'workflow-inputs-node';
/**
 * 统一管理节点和连线 ID 的生成，确保一致性。
 */
const IdGenerator = {
    inputsNode: (): string => INPUTS_NODE_ID,
    tuumNode: (tuum: TuumConfig): string => tuum.configId,
    edge: (conn: WorkflowConnection): string =>
    {
        // 在生成 Edge ID 时，需要确定其 source 节点的实际 ID
        const sourceNodeId = conn.source.tuumId === WORKFLOW_INPUT_SOURCE_ID
            ? INPUTS_NODE_ID
            : conn.source.tuumId;
        return `e-${sourceNodeId}-${conn.source.endpointName}-${conn.target.tuumId}-${conn.target.endpointName}`;
    }
};

// --- 节点和连线工厂函数 ---

/**
 * 封装创建节点对象的逻辑。
 */
const NodeFactory = {
    createInputsNode: (inputs: string[], position: { x: number, y: number }): CustomNode => ({
        id: IdGenerator.inputsNode(),
        type: 'workflow-inputs',
        position,
        data: {inputs},
    }),

    createTuumNode: (tuum: TuumConfig, position: { x: number, y: number }): CustomNode => ({
        id: IdGenerator.tuumNode(tuum),
        type: 'tuum',
        position,
        data: {tuum},
    }),
};

/**
 * 封装创建连线对象的逻辑。
 */
const EdgeFactory = {
    createWorkflowEdge: (conn: WorkflowConnection): CustomEdge =>
    {
        const sourceNodeId = conn.source.tuumId === WORKFLOW_INPUT_SOURCE_ID
            ? IdGenerator.inputsNode()
            : conn.source.tuumId;

        return {
            id: IdGenerator.edge(conn),
            source: sourceNodeId,
            target: conn.target.tuumId,
            sourceHandle: conn.source.endpointName,
            targetHandle: conn.target.endpointName,
            animated: true,
        };
    },
};

/**
 * 一个 Composable，封装了工作流编辑器的所有状态派生和交互逻辑。
 * @param context - 包含工作流数据和 storeId 的响应式引用。
 */
export function useWorkflowEditor(context: Ref<WorkflowEditorContext>)
{
    // 1. 持久化逻辑：节点位置管理
    // 使用 context.value.storeId 来确保每个工作流的位置存储是独立的。
    const nodePositions = useScopedStorage<{ [nodeId: string]: { x: number, y: number } }>(
        context.value.storeId + '-node-positions',
        {}
    );

    // 2. 状态派生层 (Deriver)

    /**
     * 派生出供 VueFlow 使用的节点 (Node) 数组。
     * 数据源: context.value.data (即 WorkflowConfig)
     * 位置源: nodePositions (本地持久化存储)
     */
    const nodes = computed<CustomNode[]>(() =>
    {
        const workflow = context.value.data;
        if (!workflow) return [];

        const nodeId = IdGenerator.inputsNode();
        const position = nodePositions.value[nodeId] ?? {x: 50, y: 150};
        const inputsNode = NodeFactory.createInputsNode(workflow.workflowInputs, position);

        const tuumNodes = workflow.tuums.map((tuum, index) =>
        {
            const nodeId = IdGenerator.tuumNode(tuum);
            const position = nodePositions.value[nodeId] ?? {x: 350, y: index * 180};
            return NodeFactory.createTuumNode(tuum, position);
        });

        return [inputsNode, ...tuumNodes];
    });

    /**
     * 派生出供 VueFlow 使用的连线 (Edge) 数组。
     * 数据源: context.value.data.graph.connections
     */
    const edges = computed<Edge[]>(() =>
    {
        const workflow = context.value.data;
        if (!workflow?.graph?.connections) return [];

        // 先根据当前的节点计算出一个有效的节点 ID 集合
        const validNodeIds = new Set(nodes.value.map(n => n.id));

        return workflow.graph.connections
            // 过滤掉孤儿连线
            .filter(conn =>
            {
                const sourceId = conn.source.tuumId === WORKFLOW_INPUT_SOURCE_ID
                    ? IdGenerator.inputsNode()
                    : conn.source.tuumId;
                // 确保连接的源头和目标节点都实际存在
                return validNodeIds.has(sourceId) && validNodeIds.has(conn.target.tuumId);
            })
            // 将有效的连接转换为 Edge 对象
            .map(EdgeFactory.createWorkflowEdge);
    });


    // 3. 交互处理层 (Interactor)

    /**
     * 处理节点拖拽停止事件，用于持久化节点位置。
     * @param event - 节点拖拽事件对象。
     */
    const handleNodeDragStop = (event: NodeDragEvent) =>
    {
        if (event.nodes && event.nodes.length > 0)
        {
            event.nodes.forEach(node =>
            {
                nodePositions.value[node.id] = node.position;
            });
        }
    };

    /**
     * 处理用户通过拖拽创建新连接的事件。
     * @param params - 连接参数。
     */
    const handleConnect = (params: Connection) =>
    {
        const workflow = context.value.data;
        if (!workflow) return;

        // 初始化 graph 和 connections 数组（如果不存在）
        if (!workflow.graph)
        {
            workflow.graph = {enableAutoConnect: false, connections: []};
        }
        if (!workflow.graph.connections)
        {
            workflow.graph.connections = [];
        }

        const sourceNode = nodes.value.find(n => n.id === params.source);
        if (!sourceNode) return;

        let source: TuumConnectionEndpoint;
        if (params.source === INPUTS_NODE_ID)
        {
            source = {
                tuumId: WORKFLOW_INPUT_SOURCE_ID,
                endpointName: params.sourceHandle!,
            };
        }
        else
        {
            source = {
                tuumId: params.source,
                endpointName: params.sourceHandle!,
            };
        }

        const newConnection: WorkflowConnection = {
            source,
            target: {
                tuumId: params.target!,
                endpointName: params.targetHandle!,
            },
        };

        const isDuplicate = workflow.graph.connections.some(existingConn =>
            existingConn.source.tuumId === newConnection.source.tuumId &&
            existingConn.source.endpointName === newConnection.source.endpointName &&
            existingConn.target.tuumId === newConnection.target.tuumId &&
            existingConn.target.endpointName === newConnection.target.endpointName
        );

        if (!isDuplicate)
        {
            workflow.graph.connections.push(newConnection);
        }
    };

    /**
     * 处理来自 VueFlow 的边变更事件。
     * 这是响应用户删除连线（例如按 Backspace 键）的正确方式。
     * @param changes - 描述变更的 EdgeChange 对象数组。
     */
    const handleEdgesChange = (changes: EdgeChange[]) =>
    {
        console.debug('接收到 edgesChange 事件:', changes); // 增加日志，便于调试

        const workflow = context.value.data;
        if (!workflow || !workflow.graph?.connections) return;

        // 1. 从变更数组中筛选出所有 "remove" 类型的变更
        const removedEdgeChanges = changes.filter(c => c.type === 'remove');
        if (removedEdgeChanges.length === 0)
        {
            return; // 如果没有删除操作，则直接返回
        }

        // 2. 提取出所有需要被删除的边的 ID
        const removedEdgeIds = new Set(removedEdgeChanges.map(c => c.id));

        // 3. 过滤工作流数据源中的 connections 数组
        workflow.graph.connections = workflow.graph.connections.filter(conn =>
        {
            // 使用我们统一的 ID 生成器来计算每个 connection 对应的 edgeId
            const edgeId = IdGenerator.edge(conn);
            // 如果计算出的 edgeId 存在于待删除ID集合中，则过滤掉这条 connection
            return !removedEdgeIds.has(edgeId);
        });
    }

    /**
     * 处理用户删除节点的事件。
     * 暂时没有被使用。
     * @param removedNodes - 被删除的节点对象数组。
     */
    const handleNodesRemove = (removedNodes: Node[]) =>
    {
        const workflow = context.value.data;
        if (!workflow) return;

        removedNodes.forEach(node =>
        {
            if (node.type === 'tuum')
            {
                // 从 tuums 数组中删除枢机
                const index = workflow.tuums.findIndex(t => t.configId === node.id);
                if (index > -1)
                {
                    workflow.tuums.splice(index, 1);
                }
            }
            else if (node.type === 'workflow-inputs')
            {
                // 删除工作流输入节点意味着清空所有工作流输入
                // 这是一个设计决策：删除该节点等同于移除所有输入。
                // 如果 inputs 不为空，则清空
                if (workflow.workflowInputs.length > 0)
                {
                    workflow.workflowInputs = [];
                }
            }
        });
    };

    // 4. 返回公共 API
    return {
        nodes,
        edges,
        handleNodeDragStop,
        handleConnect,
        handleEdgesChange,
    };
}