<template>
  <n-scrollbar style="height: 100%;">
    <n-space vertical :size="12">
      <n-card v-if="instructions.length === 0" :bordered="false">
        <n-empty description="点击地图上的对象以开始创作" />
      </n-card>

      <IntentComponent
          v-for="instruction in instructions"
          :key="instruction.id"
          :instruction="instruction"
      />
    </n-space>
  </n-scrollbar>
</template>

<script lang="ts" setup>
import { NScrollbar, NSpace, NEmpty, NCard } from 'naive-ui';
import { storeToRefs } from 'pinia';
import { useInstructionStreamStore } from '#/stores/useInstructionStreamStore';
import IntentComponent from './IntentComponent.vue';

const instructionStore = useInstructionStreamStore();
const { instructions } = storeToRefs(instructionStore);
</script>