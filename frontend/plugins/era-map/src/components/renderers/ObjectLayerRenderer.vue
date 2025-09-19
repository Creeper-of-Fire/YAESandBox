<template>
  <!--
    这个组件将渲染任务进一步委托给了更专业的 GameObjectRenderer。
    它的职责就是遍历对象列表并为每个对象实例化一个渲染器。
  -->
  <GameObjectRenderer
      v-for="obj in layer.objects"
      :key="obj.id"
      :game-object="obj"
  />
</template>

<script lang="ts" setup>
import GameObjectRenderer from '#/components/GameObjectRenderer.vue';
import type {LogicalObjectLayer} from "#/game-logic/LogicalGameMap.ts";

// 同样，明确定义Props，接收一个 ObjectLayer 实例。
defineProps<{
  layer: LogicalObjectLayer;
}>();
</script>