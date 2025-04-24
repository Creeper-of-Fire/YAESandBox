<template>
  <MainLayout>
    <!-- Toolbar 插槽 -->
    <template #toolbar>
      <AppToolbar @toggle-panel="handleTogglePanel"/>
    </template>

    <!-- 左侧面板插槽 -->
    <template #left-panel>
      <component :is="activeLeftPanel" v-if="activeLeftPanel"/>
      <div v-else class="panel-placeholder">选择左侧面板</div>
    </template>

    <!-- 主要内容插槽 -->
    <template #main-content>
      <!-- 桌面端或移动端显示 Bubble 流时 -->
      <BlockBubbleStream
          v-if="shouldShowBubbleStream"
          :blocks="currentPathBlocksForStream"
          :min-item-size="100"
          ref="bubbleStreamRef"
      />
      <!-- 移动端显示其他面板 -->
      <component v-else-if="isMobileLayout" :is="activeMobileMainComponent" />
    </template>

    <!-- 右侧面板插槽 -->
    <template #right-panel>
      <component :is="activeRightPanel" v-if="activeRightPanel"/>
      <div v-else class="panel-placeholder">选择右侧面板</div>
    </template>

    <!-- 全局元素插槽 -->
    <template #global-elements>
      <n-spin v-if="blockStatusStore.isLoadingAction" class="global-loading-spinner">
        <template #description>
          {{ blockStatusStore.loadingActionMessage || '处理中...' }}
        </template>
      </n-spin>
    </template>

    <!-- 如果需要其他 Drawer (例如设置) -->
    <!--
    <template #drawers>
        <n-drawer v-model:show="showSettingsDrawer" placement="right">
            <SettingsPanel />
        </n-drawer>
    </template>
    -->

  </MainLayout>
</template>

<script setup lang="ts">
import {ref, computed, onMounted, watch, defineAsyncComponent, shallowRef, nextTick, onUnmounted, type CSSProperties} from 'vue';
import {
  NConfigProvider, NLayout, NLayoutHeader, NLayoutContent, NDrawer, NDrawerContent,
  NButton, NIcon, NSpin, NNotificationProvider,
  lightTheme, NGrid, NGi // 引入 Grid 和 Grid Item
} from 'naive-ui';
import {LockClosedOutline as LockClosedIcon, LockOpenOutline as LockOpenIcon} from '@vicons/ionicons5';
import {useMediaQuery} from '@vueuse/core'; // 推荐使用 @vueuse/core

//@ts-ignore
import {DynamicScroller, DynamicScrollerItem} from 'vue-virtual-scroller';
import 'vue-virtual-scroller/dist/vue-virtual-scroller.css';

// 导入 Stores
import {useTopologyStore} from '@/stores/topologyStore';
import {useBlockContentStore} from '@/stores/blockContentStore';
import {useBlockStatusStore} from './stores/blockStatusStore';
import {useUiStore} from '@/stores/uiStore';
import {useConnectionStore} from '@/stores/connectionStore';

// 导入子组件
import AppToolbar from '@/components/AppToolbar.vue';
import BlockBubble from '@/components/BlockBubble.vue';
import MainLayout from "@/components/MainLayout.vue";
import BlockBubbleStream from "@/components/BlockBubbleStream.vue";

// --- 常量定义 ---
// 使用 CSS 变量来统一定义，便于维护和在 <style> 中使用 v-bind
const SIDE_PANEL_WIDTH_DESKTOP = ref('250px'); // 桌面端侧边空白/抽屉宽度 (使用 ref 以便 v-bind)
const TOOLBAR_HEIGHT = ref('64px');           // 工具栏高度 (使用 ref 以便 v-bind)

// --- 异步加载面板组件 ---
const EntityListPanel = shallowRef(defineAsyncComponent(() => import('@/components/panels/EntityListPanel.vue')));
const GameStatePanel = shallowRef(defineAsyncComponent(() => import('@/components/panels/GameStatePanel.vue')));
const SettingsPanel = shallowRef(defineAsyncComponent(() => import('@/components/panels/SettingsPanel.vue')));
// ... 其他面板

// Store 实例
const uiStore = useUiStore();
const blockStatusStore = useBlockStatusStore();
const topologyStore = useTopologyStore();

const bubbleStreamRef = ref<InstanceType<typeof BlockBubbleStream> | null>(null); // 引用

// 计算传递给 BlockBubbleStream 的 blocks 数组
const currentPathBlocksForStream = computed(() => topologyStore.getCurrentPathNodes);

// 计算当前是否应该显示 Bubble 流
const shouldShowBubbleStream = computed(() => {
  if (!isMobileLayout.value) {
    return true; // 桌面端主内容区总是显示 Bubble 流
  } else {
    // 移动端根据 uiStore 的状态决定
    return uiStore.activeMobileComponentName === 'BubbleStream';
  }
});

// 响应式状态
const isMobileLayout = useMediaQuery('(max-width: 767.9px)');

// 监视媒体查询结果并更新 store (如果 store 需要知道)
watch(isMobileLayout, (value) => {
  uiStore.setIsMobileLayout(value); // 假设 store 有这个 action
}, { immediate: true });


// --- Computed Properties for Dynamic Components ---

const activeLeftPanel = computed(() => {
  // 从 uiStore 获取当前活动的左侧面板组件引用或名称
  // return uiStore.currentLeftPanel; // 示例
  switch (uiStore.activeLeftPanelName) { // 假设 store 存的是名称
    case 'EntityList': return EntityListPanel.value;
    case 'GameState': return GameStatePanel.value; // 示例：也可以放左边
    default: return null;
  }
});

const activeRightPanel = computed(() => {
  // 从 uiStore 获取当前活动的右侧面板组件引用或名称
  // return uiStore.currentRightPanel; // 示例
  switch (uiStore.activeRightPanelName) {
    case 'GameState': return GameStatePanel.value;
    case 'Settings': return SettingsPanel.value; // 示例：设置也可以放右边
    default: return null;
  }
});

const activeMobileMainContent = computed(() => {
  // 从 uiStore 获取移动端主区域应显示的组件
  // 默认显示 Bubble 流
  switch (uiStore.activeMobileComponentName) {
    case 'EntityList': return EntityListPanel.value;
    case 'GameState': return GameStatePanel.value;
    case 'Settings': return SettingsPanel.value;
    case 'BubbleStream': // 显式处理或作为默认值
    default: return BlockBubbleStream; // 直接引用 Bubble 流组件
  }
});


// --- Event Handlers ---
const handleTogglePanel = (payload: { panelName: string; target: 'left' | 'right' | 'mobile' | 'toggleDrawer' }) => {
  // 这个方法需要根据 payload 更新 uiStore 的状态
  console.log('Toolbar wants to toggle:', payload);
  if (isMobileLayout.value) {
    // 移动端逻辑：切换主内容区
    uiStore.setActiveMobileComponent(payload.panelName); // panelName 可能是 'BubbleStream'
  } else {
    // 桌面端逻辑：切换侧边栏
    if (payload.target === 'left') {
      uiStore.setActiveLeftPanel(payload.panelName);
    } else if (payload.target === 'right') {
      uiStore.setActiveRightPanel(payload.panelName);
    }
    // 可能还需要处理关闭逻辑，例如传入 null 或 ''
  }
  // 处理其他类型的切换，如打开设置 Drawer
  // if (payload.target === 'toggleDrawer' && payload.panelName === 'Settings') { ... }
};

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
.panel-placeholder {
  color: #aaa;
  text-align: center;
  padding-top: 20px;
  font-style: italic;
}
/* 全局 Spin 样式 */
.global-loading-spinner {
  position: fixed !important; top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: 9999 !important;
  background-color: rgba(255, 255, 255, 0.7); padding: 20px; border-radius: 8px;
}
</style>