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
        <n-input-group>
          <n-button
              :disabled="!selectedObjectId"
              type="primary"
              @click="handleEnrichObject"
          >
            丰富对象
          </n-button>
          <n-auto-complete
              v-model:value="componentTypeName"
              :get-show="() => true"
              :options="dynamicComponentOptions"
              blur-after-select
              clearable
              placeholder="或输入组件名以初始化"
              style="flex-grow: 1;"
              @select="handleInitializeComponent"
          />
        </n-input-group>
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
import InteractiveMapView from '#/components/InteractiveMapView.vue';
import InstructionStreamPanel from '#/components/creator/InstructionStreamPanel.vue';
import {useWorldStateStore} from '#/stores/useWorldStateStore';
import {useInstructionStreamStore} from '#/stores/useInstructionStreamStore';


import {InstructionType} from "#/components/creator/instruction.ts";

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

const componentTypeName = ref<string>('');
// 定义预设的组件选项
const presetComponentOptions = ref([
  { label: '物理描述', value: '物理描述' },
  { label: '游戏属性', value: '游戏属性' },
  { label: '背景故事', value: '背景故事' },
  { label: '交互行为', value: '交互行为' },
]);

const dynamicComponentOptions = computed(() => {
  const customInput = componentTypeName.value?.trim();
  const options = [];

  // 如果用户有输入，将当前输入作为第一个选项
  if (customInput) {
    options.push({
      label: `${customInput}`,
      value: customInput,
      // 使用 type: 'group' 或自定义渲染可以做得更漂亮，但这不是必须的
    });
  }

  // 添加过滤后的预设选项
  // 过滤逻辑：只显示与输入相关的，或者当输入为空时显示全部
  const filteredPresets = presetComponentOptions.value.filter(option =>
      // 如果自定义输入已经和某个预设的 value 完全一样，就不再重复显示
      option.value !== customInput

      // // 简单的模糊匹配
      // && (option.label.includes(customInput || '') || option.value.includes(customInput || ''))
  );

  options.push(...filteredPresets);

  return options;
});

// --- 事件处理 ---

/** 检查是否存在针对此对象的待处理指令，避免重复创建 */
function findExistingPendingInstruction(objectId: string): boolean {
  return instructionStore.instructions.some(i =>
      i.context.targetObjectId === objectId &&
      (i.status === 'PENDING_USER_INPUT' || i.status === 'PROPOSED')
  );
}

/** 4. 处理 "丰富对象" 按钮点击 */
function handleEnrichObject() {
  if (!selectedObjectId.value) return;
  const objectId = selectedObjectId.value;
  if (!findExistingPendingInstruction(objectId)) {
    instructionStore.createInstruction(InstructionType.ENRICH_OBJECT, { targetObjectId: objectId });
  } else {
    console.log(`Instruction for object ${objectId} already exists.`);
  }
}

/** 5. 处理 "初始化组件" 自动完成选择/回车 */
function handleInitializeComponent() {
  if (!selectedObjectId.value || !componentTypeName.value) return;
  const objectId = selectedObjectId.value;
  // 这里可以复用检查逻辑，但我们暂时假设不同类型的指令可以并存
  // 如果需要严格的“一个对象一个待处理指令”，则需要调整检查逻辑

  instructionStore.createInstruction(InstructionType.INITIALIZE_COMPONENT, {
    targetObjectId: objectId,
    componentType: componentTypeName.value.trim(),
  });

  // 清空输入框以便下次使用
  componentTypeName.value = '';
}
</script>

<style scoped>

</style>