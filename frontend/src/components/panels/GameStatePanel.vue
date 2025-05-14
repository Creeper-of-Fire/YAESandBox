<template>
  <div class="panel-container game-state-panel">
    <n-h4 prefix="bar">游戏状态 (GameState)</n-h4>
    <n-text depth="3">当前 Block: {{ currentBlockId || '未选择' }}</n-text>
    <n-divider/>
    <!-- TODO: 在这里实现 GameState 的编辑功能 -->
    <n-empty description="GameState 编辑器开发中..." style="margin-top: 20px;">
      <template #icon>
        <n-icon :component="GameControllerIcon"/>
      </template>
    </n-empty>
    <!-- 示例：监听 GameState 变化 -->
    <div v-if="gameStateChangedSignal > 0" style="margin-top: 15px; font-size: 0.8em; color: blue;">
      检测到 GameState 变化信号! (第 {{ gameStateChangedSignal }} 次)
    </div>
  </div>
</template>

<script setup lang="ts">
import {computed} from 'vue';
import {NH4, NText, NDivider, NEmpty, NIcon} from 'naive-ui';
import {GameControllerOutline as GameControllerIcon} from '@vicons/ionicons5';
import {useTopologyStore} from '@/stores/topologyStore';
import {useBlockStateListener} from '@/composables/useBlockStateListener'; // 引入 listener

const topologyStore = useTopologyStore();

// 获取当前选中的 Block ID
const currentBlockId = computed(() => topologyStore.currentPathLeafId);
const currentBlockIdRef = computed(() => topologyStore.currentPathLeafId); // 需要 ref 给 listener

// 使用 listener 监听变化信号
const {gameStateChangedSignal} = useBlockStateListener(currentBlockIdRef);

// TODO:
// 1. 根据 currentBlockId 获取 GameState (GameStateService.getApiBlocksGameState)
// 2. 设计 UI 来显示和编辑 GameState 的键值对 (可能使用 Input, Select, Checkbox 等)
// 3. 在 gameStateChangedSignal 变化时重新获取 GameState
// 4. 实现保存 GameState 的功能 (GameStateService.patchApiBlocksGameState)

</script>

<style scoped>
.panel-container {
  padding: 5px;
}

.n-h4 {
  margin-bottom: 10px;
}
</style>