<template>
  <GameLayout>
    <!-- 填充左侧边栏 -->
    <template #left-sider>
      <CharacterPanel/>
    </template>

    <!-- 填充中间内容 -->
    <template #content>
      <InteractiveMapView :game-map="logicalGameMap"/>
    </template>

    <!-- 填充右侧边栏 -->
    <template #right-sider>
      <n-flex style="height: 100%" vertical>
        <SelectionPanel/>
        <LogPanel/>
      </n-flex>
    </template>
  </GameLayout>
</template>

<script lang="ts" setup>
// 导入布局组件
import GameLayout from '#/layouts/GameLayout.vue';

// 导入要填充到布局中的内容组件
import InteractiveMapView from "#/components/creator/InteractiveMapView.vue";
import CharacterPanel from "#/components/panels/CharacterPanel.vue";
import SelectionPanel from "#/components/panels/SelectionPanel.vue";
import LogPanel from "#/components/panels/LogPanel.vue";
import {NFlex} from 'naive-ui';
import {storeToRefs} from "pinia";
import {useWorldStateStore} from "#/stores/useWorldStateStore.ts";

const worldState = useWorldStateStore()

const {logicalGameMap} = storeToRefs(worldState);
</script>

<style scoped>
</style>