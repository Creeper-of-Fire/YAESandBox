import {inject, type InjectionKey} from "vue";
import type {IGameMenu} from "./createGameMenu.ts";


/**
 * 【消费者钩子】在应用的任何地方安全地获取共享的 gameMenu 实例。
 */
export function useGameMenu()
{
    const gameMenu = inject(GameMenuKey);
    if (!gameMenu)
    {
        throw new Error("useGameMenu() must be used within a component tree where the game menu has been provided.");
    }
    return gameMenu;
}

export const GameMenuKey: InjectionKey<IGameMenu> = Symbol('GameMenu');