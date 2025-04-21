<template>
  <div class="toolbar-content">
    <n-space align="center">
      <n-button @click="persistenceStore.saveSession" :loading="persistenceStore.isSaving">保存</n-button>
      <n-button @click="triggerLoad">加载</n-button>
      <input type="file" ref="loadInputRef" @change="handleFileSelect" accept=".json" style="display: none;" />

      <n-divider vertical />

      <n-tooltip trigger="hover">
        <template #trigger>
          <n-button text @click="uiStore.showEntityList">
            <template #icon><n-icon :component="ListIcon" /></template>
          </n-button>
        </template>
        实体列表
      </n-tooltip>

      <n-tooltip trigger="hover">
        <template #trigger>
          <n-button text @click="uiStore.showGameStateEditor">
            <template #icon><n-icon :component="GameControllerIcon" /></template>
          </n-button>
        </template>
        游戏状态 (开发中)
      </n-tooltip>

      <n-divider vertical />

      <n-tooltip trigger="hover">
        <template #trigger>
          <n-button text @click="uiStore.showSettings">
            <template #icon><n-icon :component="SettingsIcon" /></template>
          </n-button>
        </template>
        设置
      </n-tooltip>

      <!-- 其他按钮 -->
    </n-space>

    <!-- SignalR 连接状态指示 -->
    <ConnectionStatus />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { NSpace, NButton, NDivider, NIcon, NTooltip } from 'naive-ui';
import { ListOutline as ListIcon, SettingsOutline as SettingsIcon, GameControllerOutline as GameControllerIcon } from '@vicons/ionicons5';
import { useUiStore } from '@/stores/uiStore';
import { usePersistenceStore } from '@/stores/persistenceStore'; // 引入持久化 Store
import ConnectionStatus from './ConnectionStatus.vue'; // 引入连接状态组件

const uiStore = useUiStore();
const persistenceStore = usePersistenceStore();
const loadInputRef = ref<HTMLInputElement | null>(null);

const triggerLoad = () => {
  loadInputRef.value?.click();
};

const handleFileSelect = (event: Event) => {
  const input = event.target as HTMLInputElement;
  if (input.files && input.files.length > 0) {
    const file = input.files[0];
    persistenceStore.loadSession(file);
    input.value = ''; // 清空，以便下次选择同一个文件也能触发 change
  }
};

// 如果需要，可以在这里定义 emit 事件，但现在直接调用 Store action 更方便
// const emit = defineEmits(['toggle-left-panel', 'toggle-right-panel']);
</script>

<style scoped>
.toolbar-content {
  display: flex;
  justify-content: space-between; /* 让状态指示器靠右 */
  align-items: center;
  width: 100%;
  height: 100%;
}
.n-button {
  font-size: 1.2em; /* 图标按钮稍微大一点 */
}
</style>