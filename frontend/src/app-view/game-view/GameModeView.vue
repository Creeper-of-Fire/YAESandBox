<template>
  <ThreeColumnLayout>
    <template #toolbar>
      <!-- Toolbar 负责导入组件引用并调用 store.setActiveComponent -->
      <AppToolbar/>
    </template>

    <!--  开发期间删除KeepAlive，方便开发  -->

    <!-- 左侧面板插槽 -->

    <template #left-panel>
      <!-- 直接渲染 store 中的组件引用 -->
      <!--      <KeepAlive>-->
      <component :is="uiStore.activeLeftComponent"/>
      <!--      </KeepAlive>-->
    </template>


    <!-- 主要内容插槽 -->
    <template #main-content>
      <!--      <KeepAlive>-->
      <component :is="currentMainComponent"/>
      <!--      </KeepAlive>-->
    </template>

    <!-- 右侧面板插槽 -->
    <template #right-panel>
      <!-- 直接渲染 store 中的组件引用 -->
      <!--      <KeepAlive>-->
      <component :is="uiStore.activeRightComponent"/>
      <!--      </KeepAlive>-->
    </template>

    <!-- 其他插槽 -->
    <template #global-elements>
      <n-spin v-if="blockStatusStore.isLoadingAction" class="global-loading-spinner">
        <template #description>
          {{ blockStatusStore.loadingActionMessage || '处理中...' }}
        </template>
      </n-spin>
    </template>
  </ThreeColumnLayout>
</template>

<script setup lang="ts">
import {watch, computed} from 'vue';
import {NSpin} from 'naive-ui';
import {useMediaQuery} from '@vueuse/core'; // 推荐使用 @vueuse/core

// 导入 Stores
import {useBlockStatusStore} from '@/features/block-bubble-stream-panel/blockStatusStore.ts';
import {useUiStore} from '@/app-view/game-view/gameUiStore.ts';
import {useConnectionStore} from '@/stores/connectionStore';

// 导入子组件
import AppToolbar from '@/components/AppToolbar.vue';
import ThreeColumnLayout from "@/layouts/ThreeColumnLayout.vue";
import BlockBubbleStream from "@/features/block-bubble-stream-panel/BlockBubbleStream.vue";

// Store 实例
const uiStore = useUiStore();
const blockStatusStore = useBlockStatusStore();

// 响应式状态
const isMobileLayout = useMediaQuery('(max-width: 767.9px)');

// 监视媒体查询结果并更新 store
watch(isMobileLayout, (value) => {
  uiStore.setIsMobileLayout(value);
}, {immediate: true});


// --- Computed Properties for Dynamic Components ---
/**
 * 计算当前主内容区域应该渲染的组件。
 * 核心逻辑：
 * - 移动端：根据 uiStore.getMobileViewComponent (它会返回左/右激活组件或 null) 决定。
 * - 桌面端：固定显示 BlockBubbleStream。
 * - 任何情况下，如果特定视图组件不存在，则回退到 BlockBubbleStream。
 */
const currentMainComponent = computed(() => {
  if (isMobileLayout.value) {
    // uiStore.getMobileViewComponent 返回的是 store 中的 shallowRef 包装的组件或 null
    const focusedComponent = uiStore.getMobileViewComponent;
    if (focusedComponent) { // 如果是 shallowRef(actualComponent)
      return focusedComponent; // 直接返回 shallowRef 实例，:is 会处理
    }
    // 如果 getMobileViewComponent 返回 null (焦点在 main)，则显示 BlockBubbleStream
  }
  // 桌面端，或移动端焦点在 main 时
  return BlockBubbleStream;
});
// --- Event Handlers ---

// 处理来自 Toolbar 的通用面板切换请求


// --- Methods ---
// (如果需要手动处理 resize，可以在这里添加 handleResize 方法)

// watch

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
  position: fixed !important;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  z-index: 9999 !important;
  background-color: rgba(255, 255, 255, 0.7);
  padding: 20px;
  border-radius: 8px;
}
</style>