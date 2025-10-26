<!-- WorkflowEmittedEventsTree.vue -->
<template>
  <n-card :bordered="false" size="small" title="对外发射事件 (API)">
    <n-text depth="3" style="font-size: 12px; display: block; margin-bottom: 12px;">
      此工作流向外部系统广播的事件列表，构成了其对外的“事件API”。
    </n-text>
    <n-tree
        v-if="treeData.length > 0"
        :data="treeData"
        :overrideDefaultNodeClickBehavior="TreeSelectOverride"
        :render-label="renderLabel"
        block-line
        default-expand-all
    />
    <n-empty v-else description="此工作流不发射任何事件"/>
  </n-card>
</template>

<script lang="tsx" setup>
import {computed, type VNode} from 'vue';
import {NEmpty, NFlex, NIcon, NPopover, NTag, NText, NTree, type TreeOption} from 'naive-ui';
import {FolderIcon} from '@yaesandbox-frontend/shared-ui/icons';
import {TreeSelectOverride} from '@yaesandbox-frontend/shared-ui/utils';
import type {EmittedEventSpec} from '../../types';

// --- 类型定义 ---

interface EventTreeNode extends TreeOption
{
  key: string;
  label: string;
  isEventNode: boolean; // 标记这是否是一个事件的终点
  eventSpec?: EmittedEventSpec; // 存储完整的事件规格
  children?: EventTreeNode[];
}

// --- Props ---

const props = defineProps<{
  events: EmittedEventSpec[];
}>();

// --- 核心转换逻辑 ---

const treeData = computed<EventTreeNode[]>(() =>
{
  const root: EventTreeNode = {key: 'root', label: 'root', isEventNode: false, children: []};
  const nodeMap = new Map<string, EventTreeNode>([['root', root]]);

  // 1. 遍历所有事件，构建树的结构
  props.events.forEach(event =>
  {
    const segments = event.address.split('.');
    let currentPath = 'root';
    segments.forEach(segment =>
    {
      const parentNode = nodeMap.get(currentPath)!;
      currentPath = `${currentPath}.${segment}`;

      let childNode = nodeMap.get(currentPath);
      if (!childNode)
      {
        childNode = {
          key: currentPath,
          label: segment,
          isEventNode: false,
          children: []
        };
        parentNode.children!.push(childNode);
        nodeMap.set(currentPath, childNode);
      }
    });
  });

  // 2. 将事件的详细规格附加到对应的树节点上
  props.events.forEach(event =>
  {
    const fullPath = `root.${event.address}`;
    const eventNode = nodeMap.get(fullPath);
    if (eventNode)
    {
      eventNode.isEventNode = true;
      eventNode.eventSpec = event;
    }
  });

  // 对每个层级的子节点按字母顺序排序
  for (const node of nodeMap.values())
  {
    if (node.children)
    {
      node.children.sort((a, b) => a.label.localeCompare(b.label));
    }
  }

  return root.children || [];
});

// --- 自定义渲染函数 ---

const renderLabel = ({option}: { option: TreeOption }): VNode =>
{
  const node = option as EventTreeNode;

  // 事件详情 Popover 的内容
  const popoverContent = () => (
      <div style={{maxWidth: '400px', fontSize: '12px'}}>
        <NFlex vertical size="small">
          <NText strong>地址: {node.eventSpec?.address}</NText>
          <NText><strong>描述:</strong> {node.eventSpec?.description || '无'}</NText>
          <NFlex align="center">
            <strong>模式:</strong>
            <NTag size="small" type={node.eventSpec?.mode === 'FullSnapshot' ? 'info' : 'success'} style={{marginLeft: '8px'}}>
              {node.eventSpec?.mode}
            </NTag>
          </NFlex>
          <NText><strong>来源符文ID:</strong> {node.eventSpec?.sourceRuneConfigId}</NText>
          <NText><strong>内容类型:</strong> {node.eventSpec?.contentSpec?.typeDefinition?.typeName || '任意'}</NText>
        </NFlex>
      </div>
  );

  const labelNode = (
      <NFlex align="center" size="small">
        {!node.isEventNode && <NIcon component={FolderIcon}/>}
        <NText strong={node.isEventNode}>{node.label}</NText>
        {node.isEventNode && (
            <NTag size="small" type="info" bordered={false} style={{marginLeft: '8px'}}>
              {node.eventSpec?.mode === 'FullSnapshot' ? '快照' : '增量'}
            </NTag>
        )}
      </NFlex>
  );

  // 如果是事件节点，则用 Popover 包裹
  if (node.isEventNode)
  {
    return (
        <NPopover trigger="hover" placement="right" to={document.body}>
          {{
            trigger: () => labelNode,
            default: popoverContent
          }}
        </NPopover>
    );
  }

  return labelNode;
};

</script>