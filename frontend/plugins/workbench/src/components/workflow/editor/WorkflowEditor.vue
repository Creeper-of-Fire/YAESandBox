<!-- src/components/workflow/editor/WorkflowEditor.vue -->
<template>
  <div class="workflow-editor-wrapper">
    <VueFlow
        v-model:edges="edges"
        v-model:nodes="nodes"
        :default-viewport="{ zoom: 1 }"
        :max-zoom="4"
        :min-zoom="0.2"
        class="vue-flow-instance"
        fit-view-on-init
        @connect="onConnect"
        @edges-remove="onEdgesRemove"
    >
      <template #node-input="props">
        <InputNode v-bind="props.data"/>
      </template>

      <template #node-tuum="props">
        <TuumNode v-bind="props"/>
      </template>

      <Background/>
      <Controls/>
    </VueFlow>
  </div>
</template>

<script lang="ts" setup>
import {ref, watchEffect} from 'vue';
import {type Connection, type Edge, type Node, VueFlow} from '@vue-flow/core'
import { Background } from '@vue-flow/background';
import { Controls } from '@vue-flow/controls';
import type {WorkflowEditorContext} from "#/components/workflow/editor/WorkflowEditorContext.ts";
import TuumNode from "#/components/workflow/editor/TuumNode.vue";
import InputNode from "#/components/workflow/editor/InputNode.vue";
import type {TuumConnectionEndpoint, WorkflowConnection} from "#/types/generated/workflow-config-api-client";
import {useThemeVars} from "naive-ui";

// 定义一个特殊的 ID，用于标识来自工作流输入的连接源
const WORKFLOW_INPUT_SOURCE_ID = '__workflow_input__';

const props = defineProps<{
  workflowContext: WorkflowEditorContext;
}>();

const nodes = ref<Node[]>([]);
const edges = ref<Edge[]>([]);

// 核心逻辑：监听 workflow config 的变化，并将其转换为 nodes 和 edges
watchEffect(() =>
{
  const workflow = props.workflowContext.data;
  if (!workflow)
  {
    nodes.value = [];
    edges.value = [];
    return;
  }

  const newNodes: Node[] = [];
  const newEdges: Edge[] = [];

  // 1. 创建工作流输入节点
  workflow.workflowInputs.forEach((inputName, index) =>
  {
    newNodes.push({
      id: `input-${inputName}`,
      type: 'input',
      position: {x: 50, y: index * 100 + 50},
      data: {label: inputName},
    });
  });

  // 2. 创建枢机节点
  workflow.tuums.forEach((tuum, index) =>
  {
    newNodes.push({
      id: tuum.configId,
      type: 'tuum',
      position: {x: 350, y: index * 180},
      data: {tuum},
    });
  });

  // 3. 创建连接线 (Edges)
  if (workflow.graph?.connections)
  {
    workflow.graph.connections.forEach(conn =>
    {
      const sourceId = conn.source.tuumId === WORKFLOW_INPUT_SOURCE_ID
          ? `input-${conn.source.endpointName}`
          : conn.source.tuumId;

      newEdges.push({
        id: `e-${sourceId}-${conn.source.endpointName}-${conn.target.tuumId}-${conn.target.endpointName}`,
        source: sourceId,
        target: conn.target.tuumId,
        sourceHandle: conn.source.endpointName,
        targetHandle: conn.target.endpointName,
        animated: true,
      });
    });
  }

  nodes.value = newNodes;
  edges.value = newEdges;
});

// 处理用户连接操作
const onConnect = (params: Connection) =>
{
  const workflow = props.workflowContext.data;
  if (!workflow) return;

  // 确保 graph 和 connections 数组存在
  if (!workflow.graph)
  {
    workflow.graph = {enableAutoConnect: false, connections: []};
  }
  if (!workflow.graph.connections)
  {
    workflow.graph.connections = [];
  }

  // 从 Vue Flow 的连接参数中解析出我们的数据结构
  const sourceNode = nodes.value.find(n => n.id === params.source);
  if (!sourceNode) return;

  let source: TuumConnectionEndpoint;

  if (sourceNode.type === 'input')
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

  // 添加到数据模型中
  workflow.graph.connections.push(newConnection);
};

// 处理用户删除连接线操作
const onEdgesRemove = (params: Edge[]) =>
{
  const workflow = props.workflowContext.data;
  if (!workflow || !workflow.graph?.connections) return;

  const removedEdgeIds = new Set(params.map(e => e.id));

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
</style>