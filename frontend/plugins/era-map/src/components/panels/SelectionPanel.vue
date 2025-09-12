<template>
  <n-card title="选中信息" :bordered="false" content-style="padding: 12px;">
    <n-scrollbar style="max-height: 300px">
      <div v-if="!hasSelection" class="empty-state">
        <n-empty description="未选中任何项目"/>
      </div>
      <div v-else>
        <!-- 显示对象 -->
        <div v-if="selectedObjects.length > 0">
          <n-h4>对象</n-h4>
          <div v-for="obj in selectedObjects" :key="obj.id" class="info-item">
            <n-tag type="success" size="small">{{ obj.type }}</n-tag>
          </div>
        </div>

        <!-- 显示场 -->
        <div v-if="selectedFields.length > 0">
          <n-h4>环境</n-h4>
          <div v-for="field in selectedFields" :key="field.name" class="info-item">
            <n-text>{{ field.name }}: {{ field.value.toFixed(2) }}</n-text>
          </div>
        </div>

        <!-- 显示粒子 -->
        <div v-if="selectedParticles.length > 0">
          <n-h4>粒子</n-h4>
          <div v-for="p in selectedParticles" :key="p.type" class="info-item">
            <n-text>{{ p.type }}: ~{{ p.count }}</n-text>
          </div>
        </div>
      </div>
    </n-scrollbar>
  </n-card>
</template>

<script lang="ts" setup>
import { useSelectionStore } from '#/game-logic/selectionStore';
import { storeToRefs } from 'pinia';
import { NCard, NScrollbar, NEmpty, NH4, NTag, NText } from 'naive-ui';

const selectionStore = useSelectionStore();
// 使用 storeToRefs 来保持响应性
const { hasSelection, selectedObjects, selectedFields, selectedParticles } = storeToRefs(selectionStore);
</script>

<style scoped>
.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100px;
}
.info-item {
  margin-bottom: 8px;
}
.n-h4 {
  margin-top: 12px;
  margin-bottom: 8px;
}
</style>