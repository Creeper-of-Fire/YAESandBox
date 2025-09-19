<!-- src/saves/PluginRoot.vue -->
<template>
  <!-- 如果有激活的存档，则渲染真正的应用主界面 -->
  <ActiveSessionView v-if="activeSlot" :key="activeSlot.id" />
  <!-- 否则，显示一个引导用户创建/加载存档的界面，它是v-show，以便各种相关的状态能正确持久化 -->
  <StartupView v-show="!activeSlot"/>
</template>

<script lang="ts" setup>
import StartupView from "#/saves/ui/StartupView.vue";
import {createAndProvideEraMapGameSaveService} from "#/saves/useEraMapSaveStore.ts";
import {computed} from "vue";
import ActiveSessionView from "#/views/ActiveSessionView.vue";

const saveService = createAndProvideEraMapGameSaveService();
const activeSlot = computed(()=>saveService.activeSlot.value)
</script>