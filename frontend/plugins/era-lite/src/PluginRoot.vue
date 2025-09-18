<!-- src/PluginRoot.vue -->
<template>
  <router-view v-if="gameMenu.activeSlot.value"/>
  <StartupView v-else/>
</template>

<script lang="ts" setup>
import StartupView from "#/features/home/StartupView.vue";
import {createAndProvideEraLiteGameMenu} from "#/stores/useEraLiteSaveStore.ts";

const gameMenu = createAndProvideEraLiteGameMenu();

defineOptions({
  name: 'era-lite:PluginRoot'
})
</script>