<template>
  <n-config-provider :theme="lightTheme" class="app-container">

    <n-notification-provider>
      <n-layout style="height: 100vh;"> <!-- 顶层布局，占据整个视口 -->

        <!-- 1. 顶部工具栏 (Header) -->
        <n-layout-header bordered class="app-header">
          <AppToolbar @toggle-left-panel="uiStore.toggleLeftPanel" @toggle-right-panel="uiStore.toggleRightPanel"/>
        </n-layout-header>

        <!-- 2. 主内容区布局 (包含侧边栏和中央内容) -->
        <n-layout has-sider position="absolute" style="top: 64px; bottom: 0;"> <!-- 高度排除 Header -->

          <!-- 2.1 左侧抽屉 (Drawer) -->
          <n-drawer
              v-model:show="uiStore.isLeftPanelOpen"
              :width="leftPanelWidth"
              placement="left"
              :trap-focus="false"
              :block-scroll="false"
              :close-on-esc="!uiStore.isLeftPanelPinned"
              :mask-closable="!uiStore.isLeftPanelPinned"
              native-scrollbar
              class="side-panel left-panel"
          >
            <n-drawer-content :title="leftPanelTitle" body-content-style="padding: 10px;">
              <template #header>
                <div class="panel-header">
                  <span>{{ leftPanelTitle }}</span>
                  <n-button text @click="uiStore.toggleLeftPanelPin">
                    <template #icon>
                      <n-icon :component="uiStore.isLeftPanelPinned ? LockClosedIcon : LockOpenIcon"/>
                    </template>
                  </n-button>
                </div>
              </template>
              <!-- 动态加载左侧面板组件 -->
              <component :is="uiStore.activeLeftPanelComponent"/>
            </n-drawer-content>
          </n-drawer>

          <!-- 2.2 中央内容区 -->
          <n-layout-content
              ref="mainContentRef"
              class="main-content-area"
              :native-scrollbar="false"
              :content-style="{ height: '100%', overflow: 'hidden' }"
          >
            <!-- 使用 DynamicScroller 渲染 BlockBubble 流 -->
            <DynamicScroller
                :items="currentPathBlocks"
                :min-item-size="100"
                class="block-scroller"
                key-field="id"
                ref="blockScrollerRef"
            >
              <template v-slot="{ item, index, active }">
                <DynamicScrollerItem
                    :item="item"
                    :active="active"
                    :size-dependencies="[
                     blockContentStore.getBlockById(item.id)?.blockContent, // 主要依赖内容
                     blockStatusStore.getBlockStatus(item.id) // 状态变化也可能影响高度（如显示错误信息）
                   ]"
                    :data-index="index"
                    :key="item.id"
                    class="block-scroller-item-wrapper"
                >
                  <!-- 真实的 BlockBubble 组件 -->
                  <BlockBubble :block-id="item.id"/>
                </DynamicScrollerItem>
              </template>
              <!-- 可选: 滚动到顶部/底部的加载提示 -->
              <!-- <template #before-all><div>...</div></template> -->
              <!-- <template #after-all><div>...</div></template> -->
            </DynamicScroller>
          </n-layout-content>

          <!-- 2.3 右侧抽屉 (Drawer) -->
          <n-drawer
              v-model:show="uiStore.isRightPanelOpen"
              :width="rightPanelWidth"
              placement="right"
              :trap-focus="false"
              :block-scroll="false"
              :close-on-esc="!uiStore.isRightPanelPinned"
              :mask-closable="!uiStore.isRightPanelPinned"
              native-scrollbar
              class="side-panel right-panel"
          >
            <n-drawer-content :title="rightPanelTitle" body-content-style="padding: 10px;">
              <template #header>
                <div class="panel-header">
                  <span>{{ rightPanelTitle }}</span>
                  <n-button text @click="uiStore.toggleRightPanelPin">
                    <template #icon>
                      <n-icon :component="uiStore.isRightPanelPinned ? LockClosedIcon : LockOpenIcon"/>
                    </template>
                  </n-button>
                </div>
              </template>
              <!-- 动态加载右侧面板组件 -->
              <component :is="uiStore.activeRightPanelComponent"/>
            </n-drawer-content>
          </n-drawer>

        </n-layout> <!-- End Main Content Layout -->

        <!-- 可选：全局加载覆盖层 -->
        <n-spin :show="blockStatusStore.isLoadingAction" class="global-loading-spinner">
          <template #description>
            {{ blockStatusStore.loadingActionMessage || '处理中...' }}
          </template>
        </n-spin>
      </n-layout> <!-- End Top Level Layout -->
    </n-notification-provider>
  </n-config-provider>
</template>

<script setup lang="ts">
import {ref, computed, onMounted, watch, defineAsyncComponent, shallowRef, nextTick} from 'vue';
import {
  NConfigProvider, NLayout, NLayoutHeader, NLayoutSider, NLayoutContent, NDrawer, NDrawerContent,
  NButton, NIcon, NSpin, NMessageProvider, NNotificationProvider, NDialogProvider,
  lightTheme, // 使用亮色主题
} from 'naive-ui';
import {LockClosedOutline as LockClosedIcon, LockOpenOutline as LockOpenIcon} from '@vicons/ionicons5';

// 导入 DynamicScroller
//@ts-ignore
import {DynamicScroller, DynamicScrollerItem} from 'vue-virtual-scroller';
import 'vue-virtual-scroller/dist/vue-virtual-scroller.css';

// 导入 Stores
import {useTopologyStore} from '@/stores/topologyStore';
import {useBlockContentStore} from '@/stores/blockContentStore';
import { useBlockStatusStore } from './stores/useBlockStatusStore';
import {useUiStore} from '@/stores/uiStore'; // 引入 UI Store
import {useConnectionStore} from '@/stores/connectionStore'; // 用于 SignalR 连接

// 导入子组件
import AppToolbar from '@/components/AppToolbar.vue';
import BlockBubble from '@/components/BlockBubble.vue';

// --- 异步加载面板组件 ---
// 使用 shallowRef 存储组件引用，避免不必要的深度响应
const EntityListPanel = shallowRef(defineAsyncComponent(() => import('@/components/panels/EntityListPanel.vue')));
const GameStatePanel = shallowRef(defineAsyncComponent(() => import('@/components/panels/GameStatePanel.vue')));
const SettingsPanel = shallowRef(defineAsyncComponent(() => import('@/components/panels/SettingsPanel.vue')));
// ... 其他面板

// --- Store 实例 ---
const topologyStore = useTopologyStore();
const blockContentStore = useBlockContentStore();
const blockStatusStore = useBlockStatusStore();
const uiStore = useUiStore(); // UI Store
const connectionStore = useConnectionStore();

// --- Refs ---
const mainContentRef = ref<HTMLElement | null>(null); // 中央内容区引用 (可能不需要)
const blockScrollerRef = ref<InstanceType<typeof DynamicScroller> | null>(null); // 滚动器引用

// --- Computed ---

// 当前路径上的 Block 节点 (用于 DynamicScroller)
const currentPathBlocks = computed(() => topologyStore.getCurrentPathNodes);

// 左/右面板标题和宽度 (可以从 uiStore 获取)
const leftPanelTitle = computed(() => uiStore.leftPanelTitle || '左侧面板');
const rightPanelTitle = computed(() => uiStore.rightPanelTitle || '右侧面板');
const leftPanelWidth = computed(() => uiStore.leftPanelWidth);
const rightPanelWidth = computed(() => uiStore.rightPanelWidth);

// --- Methods ---

// 打开/关闭面板的方法 (Toolbar 会调用)
// 现在由 uiStore 处理，Toolbar 直接调用 uiStore.toggleLeftPanel(...) 等

// --- Lifecycle and Watchers ---

onMounted(async () => {
  // 初始化流程大大简化，只调用连接方法
  console.log("App [onMounted]: 开始连接 SignalR...");
  await connectionStore.connectSignalR(); // 连接和初始化逻辑已移到 store 内部
  // 可以在这里检查 connectionStore.connectionError 来处理初始连接失败的 UI
  if (connectionStore.connectionError) {
    console.error("App [onMounted]: SignalR 初始连接失败。", connectionStore.connectionError);
    // 显示全局错误提示，例如使用 useMessage
    // message.error(`连接服务器失败: ${connectionStore.connectionError}`);
  }
});

// // 监听当前路径叶节点变化，滚动到视图
// watch(() => topologyStore.currentPathLeafId, (newLeafId, oldLeafId) => {
//   if (newLeafId && blockScrollerRef.value) {
//     // 找到新叶节点在 currentPathBlocks 数组中的索引
//     const index = currentPathBlocks.value.findIndex(node => node.id === newLeafId);
//     if (index !== -1) {
//       console.log(`App: 路径叶节点变为 ${newLeafId} (索引 ${index})，尝试滚动到视图...`);
//       // 等待 DOM 更新后滚动
//       nextTick(() => {
//         blockScrollerRef.value?.scrollToItem(index, {behavior: 'smooth', block: 'nearest'});
//         // 'nearest' 会尝试让元素尽可能少地滚动就能出现在视口中
//         // 'center' 或 'start' 可能更适合我们的场景？需要测试
//       });
//     } else {
//       console.warn(`App: 新叶节点 ${newLeafId} 在当前路径数组中未找到，无法滚动。`);
//     }
//   }
// });

// // 监听拓扑变化，确保必要的 Block 内容被加载
// watch(() => topologyStore.nodes, (newNodes, oldNodes) => {
//   if (newNodes.size > 0) {
//     console.log("App: 拓扑节点发生变化，检查并获取当前路径 Block 内容...");
//     const pathIds = topologyStore.getCurrentPathNodes.map(n => n.id);
//     pathIds.forEach(id => {
//       // 如果内容不在缓存中，或者需要强制刷新（例如拓扑重建后），则获取
//       if (!blockContentStore.getBlockById(id)) {
//         blockContentStore.fetchBlockDetails(id);
//       }
//     });
//   }
// }, {deep: false}); // 浅监听 Map 对象本身的变化

</script>

<style scoped>
.app-container {
  height: 100vh;
  width: 100vw;
  overflow: hidden; /* 防止根容器滚动 */
}

.app-header {
  height: 64px; /* Naive UI 默认高度 */
  padding: 0 20px;
  display: flex;
  align-items: center;
  position: sticky; /* 或者 fixed，取决于设计 */
  top: 0;
  z-index: 100; /* 确保在 Drawer 上方 */
}

.main-content-area {
  /* background-color: #f5f5f5; */ /* 可选的背景色 */
  /* 确保内容区有内边距，避免 Bubble 贴边 */
  /* padding: 10px; 这会导致滚动条计算问题，padding 应该加在滚动器内部或项目上 */
}

.block-scroller {
  height: 100%; /* 滚动器占满内容区 */
}

.block-scroller-item-wrapper {
  padding: 5px 15px; /* 给 Bubble 提供左右边距和上下间距 */
  box-sizing: border-box;
}


.side-panel .panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

/* 全局加载指示器样式 */
.global-loading-spinner {
  position: fixed !important; /* Naive UI Spin 默认可能是 absolute */
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  z-index: 9999; /* 最高层级 */
  background-color: rgba(255, 255, 255, 0.7);
  padding: 20px;
  border-radius: 8px;
}

/* 强制 Drawer 内容区使用系统滚动条 */
:deep(.n-drawer-body-content-wrapper) {
  overflow-y: auto !important;
}

</style>