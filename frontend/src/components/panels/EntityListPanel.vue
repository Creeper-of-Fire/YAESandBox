<template>
  <div class="panel-container entity-list-panel">
    <n-h4 prefix="bar">实体列表</n-h4>
    <n-text depth="3">当前 Block: {{ currentBlockId || '未选择' }}</n-text>
    <n-divider />
    <!-- TODO: 在这里实现实体列表的获取和显示 -->
    <n-empty description="实体列表功能开发中..." style="margin-top: 20px;">
      <template #icon>
        <n-icon :component="PeopleIcon" />
      </template>
    </n-empty>
    <!-- 示例：监听 WorldState 变化 -->
    <div v-if="worldStateChangedSignal > 0" style="margin-top: 15px; font-size: 0.8em; color: green;">
      检测到 WorldState 变化信号! (第 {{ worldStateChangedSignal }} 次)
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { NH4, NText, NDivider, NEmpty, NIcon } from 'naive-ui';
import { PeopleOutline as PeopleIcon } from '@vicons/ionicons5';
import { useTopologyStore } from '@/stores/topologyStore';
import { useBlockStateListener } from '@/composables/useBlockStateListener'; // 引入 listener

const topologyStore = useTopologyStore();

// 获取当前选中的 Block ID (通常是叶节点)
const currentBlockId = computed(() => topologyStore.currentPathLeafId);
const currentBlockIdRef = computed(() => topologyStore.currentPathLeafId); // 需要 ref 给 listener

// 使用 listener 监听变化信号
const { worldStateChangedSignal } = useBlockStateListener(currentBlockIdRef);

// TODO:
// 1. 根据 currentBlockId 获取实体列表 (EntitiesService.getApiEntities)
// 2. 使用 naive-ui 的 List 或 Table 组件显示实体
// 3. 在 worldStateChangedSignal 变化时重新获取实体列表
// 4. 实现实体的增删改查交互 (可能需要弹出 Modal 或新的面板)

</script>

<style scoped>
.panel-container {
  padding: 5px;
}
.n-h4 {
  margin-bottom: 10px;
}
</style>