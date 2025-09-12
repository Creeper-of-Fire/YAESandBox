<template>
  <GameLayout>
    <!-- 填充左侧边栏 -->
    <template #left-sider>
      <CharacterPanel />
    </template>

    <!-- 填充中间内容 -->
    <template #content>
      <TavernMap />
    </template>

    <!-- 填充右侧边栏 -->
    <template #right-sider>
      <n-flex vertical style="height: 100%">
        <SelectionPanel />
        <LogPanel />
      </n-flex>
    </template>
  </GameLayout>
</template>

<script lang="ts" setup>
// 导入布局组件
import GameLayout from '#/layouts/GameLayout.vue';

// 导入要填充到布局中的内容组件
import TavernMap from "#/components/TavernMap.vue";
import CharacterPanel from "#/components/panels/CharacterPanel.vue";
import SelectionPanel from "#/components/panels/SelectionPanel.vue";
import LogPanel from "#/components/panels/LogPanel.vue";
import { NFlex } from 'naive-ui';
</script>

<style scoped>
</style>