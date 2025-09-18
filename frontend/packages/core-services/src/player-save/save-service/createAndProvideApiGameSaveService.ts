import {createGameSaveService} from "./createGameSaveService.ts";
import {ApiScopedStorage} from "../storage/ApiScopedStorage.ts";
import {ApiSaveSlotManager} from "../storage/ApiSaveSlotManager.ts";
import {ApiProjectMetaStorage} from "../storage/ApiProjectMetaStorage.ts";
import {provide} from "vue";
import {GameSaveServiceKey} from "./injectKeys.ts";
import type {IScopedStateFactory} from "../single-save/createScopedSaveStoreFactory";
import type {IGameSaveService} from "./IGameSaveService.ts";

export interface GameSaveServiceOptions
{
    uniqueName: string;
    /**
     * 一个实现了 IScopedStateFactory 接口的实例，
     * 通常是一个 Pinia store。
     */
    stateFactory: IScopedStateFactory;
}


/**
 * 【创建器】这是一个一次性的工厂函数，负责创建 saveService 的单例。
 * 它应该在应用的顶层组件的setup环境中被调用，并且它会provide。
 */
export function createAndProvideApiGameSaveService(options: GameSaveServiceOptions): IGameSaveService
{
    const {uniqueName, stateFactory} = options;

    const scopedStorage = new ApiScopedStorage();
    const saveSlotManager = new ApiSaveSlotManager(uniqueName);
    const projectMetaStorage = new ApiProjectMetaStorage(uniqueName, scopedStorage);

    const saveService = createGameSaveService({
        saveSlotManager,
        projectMetaStorage,
    });

    if (!stateFactory.isInitialized.value)
    {
        stateFactory.initialize({
            scopedStorage: scopedStorage,
            activeSlot: saveService.activeSlot,
        });
    }

    provide(GameSaveServiceKey, saveService);

    return saveService;
}