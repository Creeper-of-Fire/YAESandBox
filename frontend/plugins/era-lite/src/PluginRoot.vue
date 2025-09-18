<!-- src/PluginRoot.vue -->
<template>
  <router-view v-if="saveService.activeSlot.value"/>
  <StartupView v-else/>
</template>

<script lang="ts" setup>
import StartupView from "#/features/home/StartupView.vue";
import {createAndProvideEraLiteGameSaveService} from "#/stores/useEraLiteSaveStore.ts";

const saveService = createAndProvideEraLiteGameSaveService();

defineOptions({
  name: 'era-lite:PluginRoot'
})
</script>