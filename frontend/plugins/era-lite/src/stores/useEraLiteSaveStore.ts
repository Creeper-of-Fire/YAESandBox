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
 如何在Pinia中使用？
 const globalStore = useEraLiteSaveStore();
 const {state: sessions, isReady: isSessionsReady} = globalStore.createState<T>(STORAGE_KEY_SESSIONS, []);
 如何在组件中使用？
 const saveService = useGameSaveService();
 function quitToMainMenu(){ saveService.quitToMainMenu(); }
 */