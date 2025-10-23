<!-- SavePresenterCard.vue -->
<template>
  <n-card title="游戏存档管理">
    <n-space vertical>
      <n-h4 style="margin: 0">当前存档: {{ activeSlot?.name || '加载中...' }}</n-h4>

      <n-divider title-placement="left">切换存档</n-divider>
      <n-list bordered hoverable>
        <n-list-item v-for="slot in slots" :key="slot.id" style="cursor: pointer;" @click="handleSlotClick(slot.id)">
          <n-thing :title="slot.name">
            <template #description>
              <n-space size="small">
                <n-tag :type="slot.type === 'snapshot' ? 'info' : 'success'" size="small">{{ getSlotTypeName(slot.type) }}</n-tag>
                <n-time :time="slot.createdAt" type="relative"/>
              </n-space>
            </template>
            <template #header-extra>
              <n-tag v-if="slot.id === activeSlotId" round size="small" type="primary">当前</n-tag>
            </template>
          </n-thing>
        </n-list-item>
      </n-list>

      <n-divider title-placement="left">创建操作</n-divider>
      <n-space>
        <n-button @click="handleCreateAutosave">开启分支</n-button>
        <n-button @click="handleCreateSnapshot">保存快照</n-button>
      </n-space>
    </n-space>
  </n-card>
</template>

<script lang="ts" setup>
import {NButton, NCard, NDivider, NH4, NList, NListItem, NSpace, NTag, NThing, NTime} from 'naive-ui';
import {useGameSavePresenterUI} from "@yaesandbox-frontend/core-services/player-save";
import {useGameSavePresenter} from "@yaesandbox-frontend/core-services/player-save";

const getSlotTypeName = (saveType: string) =>
{
  if (saveType === 'autosave')
    return '自动';
  else if (saveType === 'snapshot')
    return '快照';
  else
    return '未知';
}

const saveManager = useGameSavePresenter();

const {
  slots,
  activeSlot,
  activeSlotId,
  handleSlotClick,
  handleCreateAutosave,
  handleCreateSnapshot
} = useGameSavePresenterUI(saveManager);
</script>