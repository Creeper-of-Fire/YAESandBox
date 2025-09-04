<!-- src/PluginRoot.vue -->
<template>
  <router-view v-if="gameMenu.activeSlot.value" />
  <StartupView v-else/>
</template>

<script lang="ts" setup>
import {createEraLiteGameMenu} from "#/features/home/useEraLiteGameMenu.ts";
import {GameMenuKey} from "@yaesandbox-frontend/core-services/injectKeys";
import {provide} from "vue";
import StartupView from "#/features/home/StartupView.vue";

const gameMenu = createEraLiteGameMenu();
provide(GameMenuKey, gameMenu);

defineOptions({
  name: 'era-lite:PluginRoot'
})
</script>