<template>
  <n-layout has-sider style="height: 100%;">
    <!-- 左侧：世界画布 -->
    <n-layout-content>
      <TavernMapPlus v-if="worldState.logicalGameMap && worldState.isLoaded" :game-map="worldState.logicalGameMap" @object-click="handleObjectClick" />
      <div v-else class="loading-container">
        <n-spin size="large" />
        <n-text>正在加载世界状态...</n-text>
      </div>
    </n-layout-content>

    <!-- 右侧：指令流面板 -->
    <n-layout-sider
        bordered
        content-style="padding: 12px;"
        :width="400"
    >
      <InstructionStreamPanel />
    </n-layout-sider>
  </n-layout>
</template>

<script lang="ts" setup>
import { onMounted } from 'vue';
import { NLayout, NLayoutSider, NLayoutContent, NSpin, NText } from 'naive-ui';
import { storeToRefs } from 'pinia';

// 导入我们的UI组件和Stores
import TavernMapPlus from '#/components/creator/TavernMapPlus.vue';
import InstructionStreamPanel from '#/components/creator/InstructionStreamPanel.vue';
import { useWorldStateStore } from '#/stores/useWorldStateStore';
import { useInstructionStreamStore } from '#/stores/useInstructionStreamStore';

// 导入资产和类型
import { registry } from "#/game-render/tilesetRegistry.ts";
import initLayoutJson from '#/assets/init_layout.json';
import type { FullLayoutData } from '#/game-render/types';
import { InstructionType } from '#/game-logic/types';

// --- 初始化 Store ---
const worldState = useWorldStateStore();
const instructionStore = useInstructionStreamStore();

// --- 加载初始数据 ---
onMounted(async () => {
  // 确保资源已加载
  await registry();
  // 从JSON文件加载世界骨架
  worldState.loadInitialState(initLayoutJson as FullLayoutData);
});

// --- 事件处理 ---
/**
 * 当用户在地图上点击一个对象时，创建一个新的 "丰富对象" 指令。
 * @param objectId 被点击对象的ID
 */
function handleObjectClick(objectId: string) {
  // 检查是否已经存在针对此对象的待处理指令，避免重复创建
  const existing = instructionStore.instructions.find(i =>
      i.context.targetObjectId === objectId &&
      (i.status === 'PENDING_USER_INPUT' || i.status === 'PROPOSED')
  );

  if (!existing) {
    instructionStore.createInstruction(InstructionType.ENRICH_OBJECT, { targetObjectId: objectId });
  } else {
    // 可以加一个 message 提示用户
    console.log(`Instruction for object ${objectId} already exists.`);
  }
}
</script>

<style scoped>
.loading-container {
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  height: 100%;
  gap: 1rem;
}
</style>