import {inject, type InjectionKey} from "vue";

import type {IGameSaveService} from "./IGameSaveService.ts";


/**
 * 【消费者钩子】在应用的任何地方安全地获取共享的 saveService 实例。
 */
export function useGameSaveService()
{
    const saveService = inject(GameSaveServiceKey);
    if (!saveService)
    {
        throw new Error("useGameSaveService() must be used within a component tree where the game menu has been provided.");
    }
    return saveService;
}

export const GameSaveServiceKey: InjectionKey<IGameSaveService> = Symbol('GameSaveService');