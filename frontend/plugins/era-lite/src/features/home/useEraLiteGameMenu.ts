import {useGameMenu} from '@yaesandbox-frontend/core-services/playerSave';
import {ApiProjectMetaStorage, ApiSaveSlotManager, ApiScopedStorage} from "@yaesandbox-frontend/core-services/playerSave";
import {GameMenuKey, PluginUniqueNameKey} from "@yaesandbox-frontend/core-services/injectKeys";
import {inject, provide} from "vue";
import {useEraLiteSaveStore} from "#/features/home/useEraLiteSaveStore.ts";

/**
 * 【创建器】这是一个一次性的工厂函数，负责创建 gameMenu 的单例。
 * 它应该在应用的顶层组件中被调用。
 */
export function createEraLiteGameMenu() {
    const projectUniqueName = inject(PluginUniqueNameKey);
    if (!projectUniqueName) {
        throw new Error("Could not resolve project unique name for creating GameMenu.");
    }

    const scopedStorage = new ApiScopedStorage();
    const saveSlotManager = new ApiSaveSlotManager(projectUniqueName);
    const projectMetaStorage = new ApiProjectMetaStorage(projectUniqueName, scopedStorage);

    const gameMenu = useGameMenu({
        saveSlotManager,
        projectMetaStorage,
    });

    const saveStore = useEraLiteSaveStore();
    if (!saveStore.isInitialized) {
        saveStore.initialize({
            scopedStorage: scopedStorage,
            activeSlot: gameMenu.activeSlot,
        });
    }

    return gameMenu;
}

/**
 * 【消费者钩子】在应用的任何地方安全地获取共享的 gameMenu 实例。
 */
export function useEraLiteGameMenu() {
    const gameMenu = inject(GameMenuKey);
    if (!gameMenu) {
        throw new Error("useEraLiteGameMenu() must be used within a component tree where the game menu has been provided.");
    }
    return gameMenu;
}