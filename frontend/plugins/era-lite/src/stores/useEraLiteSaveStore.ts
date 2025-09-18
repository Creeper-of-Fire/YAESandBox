import {
    createAndProvideApiGameSaveService,
    createScopedSaveStoreFactory,
    type IGameSaveService
} from "@yaesandbox-frontend/core-services/playerSave";
import {useProjectUniqueName} from "@yaesandbox-frontend/core-services/injectKeys";

export const useEraLiteSaveStore = createScopedSaveStoreFactory('era-lite-save-store');

/**
 * 【EraLite 创建器】
 * 这是 EraLite 应用的顶层工厂函数，负责组装存档系统。
 */
export function createAndProvideEraLiteGameSaveService(): IGameSaveService
{
    const projectUniqueName = useProjectUniqueName()

    const saveStore = useEraLiteSaveStore();

    return createAndProvideApiGameSaveService({
        uniqueName: projectUniqueName,
        stateFactory: saveStore.asScopedStateFactory,
    });
}

/**
这是一个样板。
saveService负责主体的存档管理。
saveStore使用时需要先声明好使用哪个存档。

如何在组件中使用？
顶层：
<template>
 <router-view v-if="saveService.activeSlot.value"/>
 <StartupView v-else/>
</template>
<script lang="ts" setup>
import StartupView from "#/features/home/StartupView.vue";
import {createAndProvideEraLiteGameSaveService} from "#/stores/useEraLiteSaveStore.ts";
const saveService = createAndProvideEraLiteGameSaveService();
</script>
后续：
const saveService = useGameSaveService();
function quitToMainMenu(){ saveService.quitToMainMenu(); }


如何在Pinia中使用？
const globalStore = useEraLiteSaveStore();
const {state: state, isReady: isReady} = globalStore.createState<T>(STORAGE_KEY_T, []);
state直接是Ref<T>类型，然后它会自动被序列化，直接拿着修改就行了，注意别把响应式搞掉。
 */