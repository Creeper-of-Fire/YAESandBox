<template>
  <n-card title="选中信息" :bordered="false" content-style="padding: 12px;">
    <n-scrollbar style="max-height: 300px">
      <div v-if="!hasSelection" class="empty-state">
        <n-empty description="未选中任何项目"/>
      </div>
      <div v-else>
        <div v-for="(info, index) in selectionDetails" :key="index" class="info-group">
        <!-- 显示对象 -->
          <div v-if="info.type === EntityInfoType.GameObject" class="info-item">
            <n-tag type="success" size="small">{{ info.entity.type }}</n-tag>
            <!-- You can access the full entity here: info.entity -->
          </div>

        <!-- 显示场 -->
          <div v-if="info.type === EntityInfoType.Field" class="info-item">
            <n-text>{{ info.name }}: {{ info.value.toFixed(2) }}</n-text>
          </div>

        <!-- 显示粒子 -->
          <div v-if="info.type === EntityInfoType.Particle" class="info-item">
            <n-text>{{ info.particleType }}: ~{{ info.density }}</n-text>
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
import { EntityInfoType } from '#/game-logic/entity/entityInfo';

const selectionStore = useSelectionStore();
// 使用 storeToRefs 来保持响应性
const { hasSelection, selectionDetails } = storeToRefs(selectionStore);
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