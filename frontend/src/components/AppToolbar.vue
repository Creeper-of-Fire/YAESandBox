<template>
  <div class="toolbar-content">
    <n-space align="center">
      <!-- 持久化按钮 -->
      <n-button @click="persistenceStore.saveSession" :loading="persistenceStore.isSaving">保存</n-button>
      <n-button @click="triggerLoad">加载</n-button>
      <input type="file" ref="loadInputRef" @change="handleFileSelect" accept=".json" style="display: none;"/>

      <n-divider vertical/>

      <!-- 实体列表按钮 -->
      <n-tooltip trigger="hover">
        <template #trigger>
          <!-- 调用 togglePanel 辅助函数 -->
          <n-button text @click="togglePanel('left', EntityListPanel)" :type="isActive('left', EntityListPanel) ? 'primary' : 'default'">
            <template #icon>
              <n-icon :component="ListIcon"/>
            </template>
          </n-button>
        </template>
        实体列表
      </n-tooltip>

      <!-- 游戏状态按钮 -->
      <n-tooltip trigger="hover">
        <template #trigger>
          <!-- 假设游戏状态放右边 -->
          <n-button text @click="togglePanel('right', GameStatePanel)" :type="isActive('right', GameStatePanel) ? 'primary' : 'default'">
            <template #icon>
              <n-icon :component="GameControllerIcon"/>
            </template>
          </n-button>
        </template>
        游戏状态
      </n-tooltip>

      <n-divider vertical/>

      <!-- 设置按钮 -->
      <n-tooltip trigger="hover">
        <template #trigger>
          <!-- 假设设置放右边 -->
          <n-button text @click="togglePanel('right', SettingsPanel)" :type="isActive('right', SettingsPanel) ? 'primary' : 'default'">
            <template #icon>
              <n-icon :component="SettingsIcon"/>
            </template>
          </n-button>
        </template>
        设置
      </n-tooltip>

      <!-- AI配置按钮 -->
      <n-tooltip trigger="hover">
        <template #trigger>
          <n-button text @click="togglePanel('left', AiConfigEditorPanel)"
                    :type="isActive('left', AiConfigEditorPanel) ? 'primary' : 'default'">
            <template #icon>
              <n-icon :component="HardwareChipIcon"/>
            </template>
          </n-button>
        </template>
        AI配置
      </n-tooltip>

      <!-- 移动端返回主界面按钮，可能没什么必要 -->
      <!--      <n-button-->
      <!--          v-if="uiStore.isMobileLayout && uiStore.mobileFocusTarget !== 'main'"-->
      <!--          text-->
      <!--          @click="uiStore.setMobileFocusToMain"-->
      <!--          title="返回主界面"-->
      <!--      >-->
      <!--        <template #icon><n-icon :component="HomeIcon" /></template> &lt;!&ndash; 使用 Home 图标示例 &ndash;&gt;-->
      <!--      </n-button>-->

      <!-- 其他按钮 -->
    </n-space>

    <!-- SignalR 连接状态指示 -->
    <ConnectionStatus/>
  </div>
</template>

<script setup lang="ts">
import {ref, defineAsyncComponent, markRaw, type Component} from 'vue'; // 引入 Component 类型
import {NSpace, NButton, NDivider, NIcon, NTooltip} from 'naive-ui';
import {
  ListOutline as ListIcon,
  SettingsOutline as SettingsIcon,
  GameControllerOutline as GameControllerIcon,
  HardwareChipOutline as HardwareChipIcon // 引入 Home 图标
} from '@vicons/ionicons5';
import {useUiStore} from '@/app-view/game-view/gameUiStore.ts';
import {usePersistenceStore} from '@/features/block-bubble-stream-panel/persistenceStore.ts';
import ConnectionStatus from './ConnectionStatus.vue';

// --- Store ---
const uiStore = useUiStore();
const persistenceStore = usePersistenceStore();

// --- Refs ---
const loadInputRef = ref<HTMLInputElement | null>(null);

// --- 导入面板组件引用 ---
// 使用 markRaw 告诉 Vue 这些不需要深度响应式处理
const EntityListPanel = markRaw(defineAsyncComponent(() => import('@/components/panels/EntityListPanel.vue')));
const GameStatePanel = markRaw(defineAsyncComponent(() => import('@/components/panels/GameStatePanel.vue')));
const SettingsPanel = markRaw(defineAsyncComponent(() => import('@/components/panels/SettingsPanel.vue')));
const AiConfigEditorPanel = markRaw(defineAsyncComponent(() => import("@/features/ai-config-panel/AiConfigEditorPanel.vue")));
// ... 其他面板

// --- Toolbar 方法 ---

/**
 * 调用 Store 的 setActiveComponent 来切换面板。
 * @param target 目标区域 'left' 或 'right'
 * @param component 要切换的组件引用
 */
const togglePanel = (target: 'left' | 'right', component: Component) =>
{
  uiStore.setActiveComponent(target, component);
};

/**
 * 检查指定区域的激活组件是否是传入的组件。
 * 用于动态设置按钮样式（例如高亮）。
 * @param target 区域 'left' 或 'right'
 * @param component 要检查的组件引用
 * @returns boolean
 */
const isActive = (target: 'left' | 'right', component: Component): boolean =>
{
  if (target === 'left')
  {
    return uiStore.activeLeftComponent === component;
  } else
  { // target === 'right'
    return uiStore.activeRightComponent === component;
  }
  // 注意：在移动端，即使面板是激活的（例如 activeLeftComponent 不为 null），
  // 如果 mobileFocusTarget 不是 'left'，它也不会显示在主区域。
  // 但按钮的状态应该反映其逻辑上的激活状态。
};


// --- 持久化相关方法 ---
const triggerLoad = () =>
{
  loadInputRef.value?.click();
};

const handleFileSelect = (event: Event) =>
{
  const input = event.target as HTMLInputElement;
  if (input.files && input.files.length > 0)
  {
    const file = input.files[0];
    persistenceStore.loadSession(file);
    input.value = '';
  }
};

</script>

<style scoped>
.toolbar-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
  height: 100%;
  padding: 0 10px; /* 给左右留点空隙 */
}

.n-button[text] { /* 只针对文本按钮增大图标 */
  font-size: 1.3em;
}
</style>