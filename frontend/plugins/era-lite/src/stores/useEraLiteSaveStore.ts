import {createAndProvideApiGameMenu, createScopedSaveStoreFactory, type IGameMenu} from "@yaesandbox-frontend/core-services/playerSave";
import {useProjectUniqueName} from "@yaesandbox-frontend/core-services/injectKeys";

export const useEraLiteSaveStore = createScopedSaveStoreFactory('era-lite-save-store');

/**
 * 【EraLite 创建器】
 * 这是 EraLite 应用的顶层工厂函数，负责组装存档系统。
 */
export function createAndProvideEraLiteGameMenu(): IGameMenu
{
    const projectUniqueName = useProjectUniqueName()
    // 获取我们刚刚定义的 specific store 的实例
    const saveStore = useEraLiteSaveStore();

    // 调用通用工厂，将 specific store 作为依赖传入
    const gameMenu = createAndProvideApiGameMenu({
        uniqueName: projectUniqueName,
        stateFactory: saveStore.asScopedStateFactory,
    });

    return gameMenu;
}