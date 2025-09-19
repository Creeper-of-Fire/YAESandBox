<template>
  <n-layout has-sider style="height: 100%;">
    <!-- 左侧：世界画布 -->
    <n-layout-content>
      <InteractiveMapView :game-map="logicalGameMap"/>
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
import InteractiveMapView from '#/components/creator/InteractiveMapView.vue';
import InstructionStreamPanel from '#/components/creator/InstructionStreamPanel.vue';
import {useWorldStateStore} from '#/stores/useWorldStateStore';
import {useInstructionStreamStore} from '#/stores/useInstructionStreamStore';

import {InstructionType} from '#/game-logic/types';

// --- 初始化 Store ---
const worldState = useWorldStateStore();
const {allObjects,logicalGameMap} = storeToRefs(worldState);
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

</style>