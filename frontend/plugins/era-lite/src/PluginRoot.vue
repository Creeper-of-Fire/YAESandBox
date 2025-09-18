<!-- src/PluginRoot.vue -->
<template>
  <router-view v-if="activeSlot"/>
  <StartupView v-show="!activeSlot"/>
</template>

<script lang="ts" setup>
import StartupView from "#/features/home/StartupView.vue";
import {createAndProvideEraLiteGameSaveService} from "#/stores/useEraLiteSaveStore.ts";
import {computed} from "vue";

const saveService = createAndProvideEraLiteGameSaveService();
const activeSlot = computed(()=>saveService.activeSlot.value)
</script>