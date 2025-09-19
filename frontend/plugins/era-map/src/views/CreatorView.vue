<template>
  <n-layout has-sider style="height: 100%;">
    <!-- 左侧：世界画布 -->
    <n-layout-content>
      <TavernMapPlus v-if="worldState.logicalGameMap && worldState.isLoaded" :game-map="worldState.logicalGameMap"/>
      <div v-else class="loading-container">
        <n-spin size="large"/>
        <n-text>正在加载世界状态...</n-text>
      </div>
    </n-layout-content>

    <!-- 右侧：指令流面板 -->
    <n-layout-sider
        :width="400"
        bordered
        content-style="padding: 12px;"
    >
      <n-space vertical>
        <n-select
            v-model:value="selectedObjectId"
            :options="objectOptions"
            clearable
            placeholder="请选择要丰富的对象"
        />
        <n-button
            :disabled="!selectedObjectId"
            block
            type="primary"
            @click="handleAddIntent"
        >
          添加丰富指令
        </n-button>
        <n-divider/>
        <InstructionStreamPanel/>
      </n-space>
    </n-layout-sider>
  </n-layout>
</template>

<script lang="ts" setup>
import {computed, onMounted, ref} from 'vue';
import {NLayout, NLayoutContent, NLayoutSider, NSpin, NText} from 'naive-ui';
import {storeToRefs} from 'pinia';

// 导入我们的UI组件和Stores
import TavernMapPlus from '#/components/creator/TavernMapPlus.vue';
import InstructionStreamPanel from '#/components/creator/InstructionStreamPanel.vue';
import {useWorldStateStore} from '#/stores/useWorldStateStore';
import {useInstructionStreamStore} from '#/stores/useInstructionStreamStore';

// 导入资产和类型
import {registry} from "#/game-render/tilesetRegistry.ts";
// @ts-ignore
import initLayoutJson from '#/assets/init_layout.json';
import type {FullLayoutData} from '#/game-render/types';
import {InstructionType} from '#/game-logic/types';

// --- 初始化 Store ---
const worldState = useWorldStateStore();
const {allObjects} = storeToRefs(worldState);
const instructionStore = useInstructionStreamStore();

// 选择器
const selectedObjectId = ref<string | null>(null);
const objectOptions = computed(() =>
{
  return allObjects.value.map(obj => ({
    label: `${obj.type} (${obj.id.slice(0, 4)})`,
    value: obj.id,
  }));
});

// --- 加载初始数据 ---
onMounted(async () =>
{
  // 确保资源已加载
  await registry();
  // 从JSON文件加载世界骨架
  // @ts-ignore
  worldState.loadInitialState(initLayoutJson as FullLayoutData);
});

// --- 事件处理 ---
/**
 * 当用户点击 "添加意图" 按钮时，为选中的对象创建一个新指令。
 */
function handleAddIntent()
{
  if (!selectedObjectId.value) return; // 防御性检查

  const objectId = selectedObjectId.value;

  // 检查是否已经存在针对此对象的待处理指令，避免重复创建 (逻辑与之前相同)
  const existing = instructionStore.instructions.find(i =>
      i.context.targetObjectId === objectId &&
      (i.status === 'PENDING_USER_INPUT' || i.status === 'PROPOSED')
  );

  if (!existing)
  {
    instructionStore.createInstruction(InstructionType.ENRICH_OBJECT, {targetObjectId: objectId});
  }
  else
  {
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