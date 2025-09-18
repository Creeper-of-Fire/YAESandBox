<!-- src/saves/PluginRoot.vue -->
<template>
  <router-view v-if="activeSlot"/>
  <StartupView v-show="!activeSlot"/>
</template>

<script lang="ts" setup>
import StartupView from "#/saves/ui/StartupView.vue";
import {createAndProvideEraMapGameSaveService} from "#/saves/useEraMapSaveStore.ts";
import {computed} from "vue";

const saveService = createAndProvideEraMapGameSaveService();
const activeSlot = computed(()=>saveService.activeSlot.value)
</script>