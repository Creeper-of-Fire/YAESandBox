<template>
  <draggable
      :model-value="modelValue || []"
      class="draggable-tree-group"
      v-bind="$attrs"
      @update:model-value="value => emit('update:modelValue', value)"
  >
    <div v-for="node in modelValue" :key="node.key" :data-drag-id="node.key"
         :data-drag-type="getNodeConfigType(node)"
         class="tree-node-wrapper"
    >
      <!-- 1. 作用域插槽，将渲染权交给父组件 -->
      <!-- 我们传递了节点数据、层级和展开状态 -->
      <slot
          :is-expanded="isExpanded(node.key)"
          :level="level"
          :node="node"
          :toggle-expand="() => toggleExpand(node.key)"
          name="node"
      ></slot>

      <!-- 2. 递归渲染子节点 -->
      <n-collapse-transition :show="isExpanded(node.key)">
        <div v-if="!node.isLeaf" class="children-container">
          <DraggableTree
              :model-value="node.children || []"
              :level="(level || 0) + 1"
              v-bind="$attrs"
              @update:model-value="newChildren => handleChildrenUpdate(node, newChildren)"
          >
            <template #node="slotProps:any">
              <slot name="node" v-bind="slotProps"></slot>
            </template>
          </DraggableTree>
        </div>
      </n-collapse-transition>
    </div>

    <!-- ✨ 如果当前列表为空，则显示一个空的放置区 -->
    <n-empty
        v-if="modelValue?.length === 0"
        class="empty-drop-zone"
        description="拖拽到此处"
        size="small"
    />
  </draggable>
</template>

<script generic="T extends { key: string; isLeaf: boolean; children?: T[]; configType: any; }" lang="ts" setup>
import {ref, watch} from 'vue';
import {VueDraggable as draggable} from 'vue-draggable-plus';
import {NCollapseTransition} from 'naive-ui';

defineOptions({
  inheritAttrs: false
});

const props = withDefaults(defineProps<{
  modelValue?: T[];
  level?: number;
}>(), {
  level: 0
});

const emit = defineEmits<{
  (e: 'update:modelValue', value: T[]): void;
}>();

// --- 展开/折叠状态管理 ---
const expandedKeys = ref<Set<string>>(new Set());
const isExpanded = (key: string) => expandedKeys.value.has(key);
const toggleExpand = (key: string) =>
{
  if (expandedKeys.value.has(key))
  {
    expandedKeys.value.delete(key);
  }
  else
  {
    expandedKeys.value.add(key);
  }
};

// 默认展开所有文件夹
const initializeExpandedState = (nodes: T[]) =>
{
  nodes.forEach(node =>
  {
    if (!node.isLeaf)
    {
      expandedKeys.value.add(node.key);
      if (node.children)
      {
        initializeExpandedState(node.children);
      }
    }
  });
};
watch(() => props.modelValue, (newVal) =>
{
  expandedKeys.value.clear();
  initializeExpandedState(newVal ?? []);
}, {immediate: true, deep: true});


function handleChildrenUpdate(parentNode: T, newChildren: T[]) {
  if (!props.modelValue) return;

  // 1. 在当前组件的 modelValue 中找到需要更新的节点
  const nodeToUpdate = props.modelValue.find(n => n.key === parentNode.key);

  if (nodeToUpdate) {
    // 2. 更新该节点的 children 属性
    nodeToUpdate.children = newChildren;
  }

  // 3. 关键！发出一个 update:modelValue 事件，通知父组件自己的整个列表已经改变
  //    使用 [...props.modelValue] 创建一个新的数组引用，以确保 Vue 的响应式系统能够检测到变化。
  emit('update:modelValue', [...props.modelValue]);
}

// --- `vue-draggable-plus` 事件处理 ---

// 辅助函数，用于在模板中获取 configType
function getNodeConfigType(node: T)
{
  return node.isLeaf ? node.configType : 'folder';
}

</script>

<style scoped>
/* 递归渲染的子节点容器 */
.children-container {
  padding-left: 20px; /* 为所有子节点提供一致的缩进 */
  position: relative;
}
</style>