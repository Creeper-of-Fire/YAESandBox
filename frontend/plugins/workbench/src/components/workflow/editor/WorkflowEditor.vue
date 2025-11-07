<!-- src/components/workflow/editor/WorkflowEditor.vue -->
<template>
  <div class="workflow-editor-wrapper">
    <!-- 暂时不实现 @nodes-remove="onNodesRemove" -->
    <VueFlow
        :default-viewport="{ zoom: 1 }"
        :edges="edges"
        :max-zoom="4"
        :min-zoom="0.2"
        :multiSelectionKeyCode="'Control'"
        :nodes="nodes"
        class="vue-flow-instance"
        fit-view-on-init
        @connect="onConnect"
        @edges-remove="onEdgesRemove"
        @node-drag-stop="onNodeDragStop"
    >
      <template #node-workflow-input="props">
        <InputNode v-bind="props"/>
      </template>

      <template #node-tuum="props">
        <TuumNode v-bind="props"/>
      </template>

      <Background/>
      <Controls/>

      <div class="custom-controls-info">
        <n-text depth="3">
          选中连线后，按 <strong>Backspace</strong> 键可删除。
          目前不支持删除节点的逻辑。
        </n-text>
        <n-text depth="3">
          按 <strong>Control</strong> 键可以同时选择多个节点。
        </n-text>
      </div>
    </VueFlow>
  </div>
</template>

<script lang="ts" setup>
import {computed} from 'vue'; // 引入 computed
import {type Connection, type Edge, type Node, type NodeDragEvent, VueFlow} from '@vue-flow/core'
import {Background} from '@vue-flow/background';
import {Controls} from '@vue-flow/controls';
import type {WorkflowEditorContext} from "#/components/workflow/editor/WorkflowEditorContext.ts";
import TuumNode from "#/components/workflow/editor/TuumNode.vue";
import InputNode from "#/components/workflow/editor/InputNode.vue";
import type {TuumConnectionEndpoint, WorkflowConnection} from "#/types/generated/workflow-config-api-client";
import {useThemeVars} from "naive-ui";
import {useScopedStorage} from "@yaesandbox-frontend/core-services/composables";
import {WORKFLOW_INPUT_SOURCE_ID} from "#/utils/constants.ts";

const props = defineProps<{
  workflowContext: WorkflowEditorContext;
}>();

const nodePositions = useScopedStorage<{ [nodeId: string]: { x: number, y: number } }>(
    props.workflowContext.storeId + 'node-positions',
    {}
);

// --- 2. 核心重构：使用 computed 派生视图状态 ---

// 从 watchEffect 改为 computed，这是更健壮的 Vue 模式
const nodes = computed<Node[]>(() =>
{
  const workflow = props.workflowContext.data;
  if (!workflow) return [];

  const newNodes: Node[] = [];

  // 创建工作流输入节点
  workflow.workflowInputs.forEach((inputName, index) =>
  {
    const nodeId = `input-${inputName}`;
    newNodes.push({
      id: nodeId,
      type: 'workflow-input',
      position: nodePositions.value[nodeId] ?? {x: 50, y: index * 100 + 50},
      data: {label: inputName},
    });
  });

  // 创建枢机节点
  workflow.tuums.forEach((tuum, index) =>
  {
    const nodeId = tuum.configId;
    newNodes.push({
      id: nodeId,
      type: 'tuum',
      position: nodePositions.value[nodeId] ?? {x: 350, y: index * 180},
      data: {tuum},
    });
  });

  return newNodes;
});

// 创建连接线 (Edges)
const edges = computed<Edge[]>(() =>
{
  const workflow = props.workflowContext.data;
  if (!workflow?.graph?.connections) return [];

  return workflow.graph.connections.map(conn =>
  {
    const sourceId = conn.source.tuumId === WORKFLOW_INPUT_SOURCE_ID
        ? `input-${conn.source.endpointName}`
        : conn.source.tuumId;

    return {
      id: `e-${sourceId}-${conn.source.endpointName}-${conn.target.tuumId}-${conn.target.endpointName}`,
      source: sourceId,
      target: conn.target.tuumId,
      sourceHandle: conn.source.endpointName,
      targetHandle: conn.target.endpointName,
      animated: true,
    };
  });
});

/**
 * 删除节点的事件处理器 (未使用)
 * @deprecated
 * @param removedNodes
 */
const onNodesRemove = (removedNodes: Node[]) =>
{
  const workflow = props.workflowContext.data;
  if (!workflow) return;

  const removedNodeIds = new Set(removedNodes.map(n => n.id));

  // 遍历被删除的节点，从源数据中移除它们
  removedNodes.forEach(node =>
  {
    if (node.type === 'tuum')
    {
      // 从 tuums 数组中删除
      const index = workflow.tuums.findIndex(t => t.configId === node.id);
      if (index > -1)
      {
        workflow.tuums.splice(index, 1);
      }
    }
    else if (node.type === 'input')
    {
      // 从 workflowInputs 数组中删除
      const inputName = node.data.label;
      const index = workflow.workflowInputs.indexOf(inputName);
      if (index > -1)
      {
        workflow.workflowInputs.splice(index, 1);
      }
    }
  });

  // 同样重要的是，删除与这些节点相关的所有连接
  if (workflow.graph?.connections)
  {
    workflow.graph.connections = workflow.graph.connections.filter(conn =>
    {
      const sourceNodeId = conn.source.tuumId === WORKFLOW_INPUT_SOURCE_ID
          ? `input-${conn.source.endpointName}`
          : conn.source.tuumId;
      const targetNodeId = conn.target.tuumId;

      // 如果连接的源或目标节点在被删除的节点ID集合中，则过滤掉这条连接
      return !removedNodeIds.has(sourceNodeId) && !removedNodeIds.has(targetNodeId);
    });
  }
};


const onNodeDragStop = (event: NodeDragEvent) =>
{
  if (event.nodes && event.nodes.length > 0)
  {
    event.nodes.forEach(node =>
    {
      nodePositions.value[node.id] = node.position;
    });
  }
};

const onConnect = (params: Connection) =>
{
  const workflow = props.workflowContext.data;
  if (!workflow) return;

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
  if (sourceNode.type === 'workflow-input')
  {
    source = {
      tuumId: WORKFLOW_INPUT_SOURCE_ID,
      endpointName: params.sourceHandle!,
    };
  }
  else
  {
    source = {
      tuumId: params.source!,
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

  workflow.graph.connections.push(newConnection);
};

const onEdgesRemove = (removedEdges: Edge[]) =>
{
  const workflow = props.workflowContext.data;
  if (!workflow || !workflow.graph?.connections) return;

  const removedEdgeIds = new Set(removedEdges.map(e => e.id));

  workflow.graph.connections = workflow.graph.connections.filter(conn =>
  {
    const sourceId = conn.source.tuumId === WORKFLOW_INPUT_SOURCE_ID
        ? `input-${conn.source.endpointName}`
        : conn.source.tuumId;
    const edgeId = `e-${sourceId}-${conn.source.endpointName}-${conn.target.tuumId}-${conn.target.endpointName}`;
    return !removedEdgeIds.has(edgeId);
  });
}

const themeVars = useThemeVars()
</script>

<style scoped>
.workflow-editor-wrapper {
  width: 100%;
  height: calc(100vh - 180px); /* 减去头部和一些边距的高度 */
  border: 1px solid v-bind('themeVars.borderColor');
  border-radius: 4px;
}

:deep(.vue-flow__node.selected) {
  box-shadow: 0 0 0 2px v-bind('themeVars.primaryColor');
}
</style>