<template>
  <!-- 虚拟滚动容器 -->
  <DynamicScroller
      :items="currentPathBlocks"
      :min-item-size=100
      class="block-scroller"
      key-field="id"
      ref="blockScrollerRef"
  >
    <!-- 定义每个滚动项的渲染模板 -->
    <template v-slot="{ item, index, active }">
      <!-- 虚拟滚动项包装器 -->
      <DynamicScrollerItem
          :item="item"
          :active="active"
          :size-dependencies="[ getSizeDependencies(item.id) ]"
          :data-index="index"
          :key="item.id"
          class="block-scroller-item-wrapper"
      >
        <!-- 实际渲染的 BlockBubble 组件 -->
        <BlockBubble :block-id="item.id"/>
      </DynamicScrollerItem>
    </template>

    <!-- 可选: 滚动到顶部/底部的加载提示 -->
    <!--
    <template #before-all><div>正在加载历史记录...</div></template>
    <template #after-all><div></div></template>
    -->
  </DynamicScroller>
</template>

<script setup lang="ts">
import {ref, computed, watch, nextTick} from 'vue';
// 导入 DynamicScroller 相关组件
// @ts-ignore 因为 vue-virtual-scroller 的类型定义可能不完美
import {DynamicScroller, DynamicScrollerItem} from 'vue-virtual-scroller';
import 'vue-virtual-scroller/dist/vue-virtual-scroller.css'; // 确保样式被引入

// 导入子组件
import BlockBubble from '@/app-game/features/block-bubble-stream-panel/BlockBubble.vue';

// 导入 Stores
import {useTopologyStore} from '@/app-game/features/block-bubble-stream-panel/topologyStore.ts';
import {useBlockContentStore} from '@/app-game/features/block-bubble-stream-panel/blockContentStore.ts';
import {useBlockStatusStore} from '@/app-game/features/block-bubble-stream-panel/blockStatusStore.ts';

// --- Store 实例 ---
const topologyStore = useTopologyStore();
const blockContentStore = useBlockContentStore();
const blockStatusStore = useBlockStatusStore();

// --- Refs ---
// 对 DynamicScroller 组件实例的引用，用于编程式滚动
const blockScrollerRef = ref<InstanceType<typeof DynamicScroller> | null>(null);

// --- Computed ---
// 当前路径的叶子节点 ID，用于触发滚动
const currentLeafId = computed(() => topologyStore.currentPathLeafId);
// 当前路径上的 blocks
const currentPathBlocks = computed(() => topologyStore.getCurrentPathNodes);

// --- Methods ---

/**
 * 获取指定 Block ID 的尺寸依赖项。
 * DynamicScrollerItem 的 :size-dependencies 需要一个数组，
 * 当数组中的任何值发生变化时，它会重新计算该项的高度。
 * 我们主要关心内容和状态的变化。
 * @param blockId Block 的 ID
 * @returns 包含内容和状态的数组，或一个简单值（如 blockId 本身）以简化依赖
 */
const getSizeDependencies = (blockId: string) =>
{
  // 返回一个能代表内容和状态变化的值。
  // 简单起见，可以返回一个包含两者信息的对象或数组。
  // 注意：过于复杂的依赖或频繁变化可能影响性能。
  // 选项1：返回包含内容的简单对象（如果状态变化不频繁影响高度）
  // return blockContentStore.getBlockById(blockId)?.blockContent;

  // 选项2：返回组合值（更精确，但可能更频繁触发计算）
  // return [
  //   blockContentStore.getBlockById(blockId)?.blockContent,
  //   blockStatusStore.getBlockStatus(blockId) // 假设状态对象是响应式的
  // ];

  // 选项3：返回 blockId 本身。这意味着只有当 blockId 列表变化时才会检查。
  // 如果 BlockBubble 内部内容变化导致高度变化，需要确保 BlockBubble 能正确通知父级或 Scroller。
  // 或者依赖 DynamicScroller 的自动高度调整（如果有）。
  // 为了平衡，我们监听内容和状态。
  const content = blockContentStore.getBlockById(blockId)?.blockContent;
  const status = blockStatusStore.getBlockStatus(blockId);
  // 使用 JSON.stringify 将对象转换为字符串，以便 Vue 的依赖跟踪能检测到对象内部变化
  // 注意：这可能对性能有一定影响，如果状态对象很大或变化非常频繁
  return `${JSON.stringify(content)}-${JSON.stringify(status)}`;
};


/**
 * 滚动到指定的 Block 索引。
 * @param index 要滚动到的项目在 `props.blocks` 数组中的索引。
 */
const scrollToBlockIndex = (index: number) =>
{
  if (blockScrollerRef.value && index >= 0 && index < currentPathBlocks.value.length)
  {
    console.log(`BlockBubbleStream: 尝试滚动到索引 ${index}...`);
    // 使用 'smooth' 实现平滑滚动，'auto' 为即时滚动
    // 'block: 'end'' 尝试将项目的底部与滚动容器的底部对齐，适合查看最新消息
    // 'block: 'nearest'' 滚动最小距离使其可见
    blockScrollerRef.value.scrollToItem(index, {behavior: 'smooth', block: 'end'});
  } else
  {
    console.warn(`BlockBubbleStream: 无法滚动到索引 ${index}。滚动器引用: ${!!blockScrollerRef.value}, 索引范围: 0-${currentPathBlocks.value.length - 1}`);
  }
};

// --- Watchers ---

// 监听当前路径叶子节点 ID 的变化
watch(currentLeafId, (newLeafId, oldLeafId) =>
{
  // 仅在叶子节点 ID 发生变化且不是 null 时执行
  if (newLeafId && newLeafId !== oldLeafId)
  {
    console.log(`BlockBubbleStream: 检测到叶子节点变化 -> ${newLeafId}`);
    // 找到新叶子节点在当前 blocks 数组中的索引
    const index = currentPathBlocks.value.findIndex(block => block.id === newLeafId);

    if (index !== -1)
    {
      // 等待 DOM 更新后再执行滚动操作，确保目标项已渲染
      nextTick(() =>
      {
        scrollToBlockIndex(index);
      });
    } else
    {
      console.warn(`BlockBubbleStream: 新叶子节点 ${newLeafId} 在当前 blocks 数组中未找到，无法滚动。`);
      // 可能情况：blocks 数组尚未更新，或新节点确实不在当前路径上
      // 可以考虑在此处添加重试逻辑或等待 blocks 更新
    }
  }
}, {
  flush: 'post' // 确保在 DOM 更新之后触发 watcher 回调
});

// 可选：监听 blocks 数组本身的变化，例如在路径切换导致数组完全替换时滚动到底部
watch(() => currentPathBlocks.value, (newBlocks, oldBlocks) =>
{
  // 判断是否是显著变化（例如，数组引用改变或长度显著变化）
  if (newBlocks && oldBlocks && newBlocks !== oldBlocks)
  {
    // 路径切换或大量添加时，滚动到底部
    const lastIndex = newBlocks.length - 1;
    if (lastIndex >= 0)
    {
      nextTick(() =>
      {
        // 切换路径时可能用 'auto' 更快到达底部
        if (blockScrollerRef.value)
        {
          console.log(`BlockBubbleStream: blocks 数组变化，滚动到末尾索引 ${lastIndex}`);
          blockScrollerRef.value.scrollToItem(lastIndex, {behavior: 'auto', block: 'end'});
        }
      });
    }
  }
}, {deep: false}); // 浅监听数组引用变化即可

// --- Lifecycle Hooks ---
// onMounted(() => {
//   // 可以在挂载后尝试滚动到底部一次
//   const lastIndex = props.blocks.length - 1;
//     if (lastIndex >= 0) {
//         nextTick(() => {
//             scrollToBlockIndex(lastIndex);
//         });
//     }
// });

// 如果需要在父组件中调用滚动方法，可以通过 defineExpose 暴露
defineExpose({
  scrollToBlockIndex
});

</script>

<style scoped>
.block-scroller {
  height: 100%; /* 滚动器需要明确的高度才能工作，通常由父容器提供 */
  width: 100%;
  overflow-y: auto; /* 允许滚动 */
  overflow-x: hidden; /* 通常不需要水平滚动 */
  /* background-color: #eee; */ /* 可选：给滚动区域一个背景色方便调试 */
}

.block-scroller-item-wrapper {
  /* 为每个 BlockBubble 提供一些垂直和水平间距 */
  padding: 8px 15px; /* 上下 8px，左右 15px */
  box-sizing: border-box; /* 内边距包含在元素尺寸内 */
}
</style>