<template>
  <MainLayout>
    <template #toolbar>
      <!-- Toolbar 负责导入组件引用并调用 store.setActiveComponent -->
      <AppToolbar />
    </template>

    <!-- 左侧面板插槽 -->
    <template #left-panel>
      <!-- 直接渲染 store 中的组件引用 -->
      <component :is="uiStore.activeLeftComponent" v-if="uiStore.activeLeftComponent"/>
    </template>

    <!-- 主要内容插槽 -->
    <template #main-content>
      <template v-if="isMobileLayout">
        <!-- 移动端根据 getter 决定显示哪个已激活的组件 -->
        <component :is="uiStore.getMobileViewComponent" v-if="uiStore.getMobileViewComponent"/>
        <!-- 如果 getter 返回 null，显示 BubbleStream -->
        <BlockBubbleStream v-else />
      </template>
      <template v-else>
        <!-- 桌面端固定显示 BubbleStream -->
        <BlockBubbleStream />
      </template>
    </template>

    <!-- 右侧面板插槽 -->
    <template #right-panel>
      <!-- 直接渲染 store 中的组件引用 -->
      <component :is="uiStore.activeRightComponent" v-if="uiStore.activeRightComponent"/>
    </template>

    <!-- 其他插槽 -->
    <template #global-elements>
      <n-spin v-if="blockStatusStore.isLoadingAction" class="global-loading-spinner">
        <template #description>
          {{ blockStatusStore.loadingActionMessage || '处理中...' }}
        </template>
      </n-spin>
    </template>
  </MainLayout>
</template>

<script setup lang="ts">
import {ref, onMounted, watch, onUnmounted} from 'vue';
import {
  NSpin// 引入 Grid 和 Grid Item
} from 'naive-ui';
import {useMediaQuery} from '@vueuse/core'; // 推荐使用 @vueuse/core

// 导入 Stores
import {useBlockStatusStore} from './stores/blockStatusStore';
import {useUiStore} from '@/stores/uiStore';
import {useConnectionStore} from '@/stores/connectionStore';

// 导入子组件
import AppToolbar from '@/components/AppToolbar.vue';
import MainLayout from "@/components/MainLayout.vue";
import BlockBubbleStream from "@/components/BlockBubbleStream.vue";

// Store 实例
const uiStore = useUiStore();
const blockStatusStore = useBlockStatusStore();
const connectionStore = useConnectionStore();

// 响应式状态
const isMobileLayout = useMediaQuery('(max-width: 767.9px)');

// 监视媒体查询结果并更新 store
watch(isMobileLayout, (value) => {
  uiStore.setIsMobileLayout(value);
}, { immediate: true });


// --- Computed Properties for Dynamic Components ---

// --- Event Handlers ---

// 处理来自 Toolbar 的通用面板切换请求


// --- Methods ---
// (如果需要手动处理 resize，可以在这里添加 handleResize 方法)

// --- Lifecycle and Watchers ---

onMounted(async () => {
  console.log("App [onMounted]: 组件挂载，开始连接 SignalR...");
  await connectionStore.connectSignalR();
  if (connectionStore.connectionError) {
    console.error("App [onMounted]: SignalR 初始连接失败。", connectionStore.connectionError);
    // 显示错误提示...
  }
});

onUnmounted(() => {
  // 清理工作（例如移除 resize 监听器，如果添加了的话）
  console.log("App [onUnmounted]: 组件卸载。");
});

// // 监听当前路径叶节点变化，滚动到视图
// watch(() => topologyStore.currentPathLeafId, (newLeafId) => {
//   if (newLeafId && blockScrollerRef.value) {
//     const index = currentPathBlocks.value.findIndex(node => node.id === newLeafId);
//     if (index !== -1) {
//       console.log(`App: 路径叶节点变为 ${newLeafId} (索引 ${index})，尝试滚动到视图...`);
//       nextTick(() => {
//         blockScrollerRef.value?.scrollToItem(index, {behavior: 'smooth', block: 'end'});
//       });
//     } else {
//       console.warn(`App: 新叶节点 ${newLeafId} 在当前路径数组中未找到，无法滚动。`);
//     }
//   }
// }, {flush: 'post'});
//
// // 监听当前路径变化，加载内容
// watch(() => topologyStore.getCurrentPathNodes, (newPathNodes) => {
//   console.log("App: 当前路径节点变化，检查并获取 Block 内容...");
//   newPathNodes.forEach(node => {
//     if (!blockContentStore.getBlockById(node.id)) {
//       console.log(`App: 路径节点 ${node.id} 内容缺失，尝试获取...`);
//       blockContentStore.fetchAllBlockDetails(node.id);
//     }
//   });
// }, {deep: false, immediate: true});

</script>

<style scoped>
/* 全局 Spin 样式 */
.global-loading-spinner {
  position: fixed !important; top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: 9999 !important;
  background-color: rgba(255, 255, 255, 0.7); padding: 20px; border-radius: 8px;
}
</style>