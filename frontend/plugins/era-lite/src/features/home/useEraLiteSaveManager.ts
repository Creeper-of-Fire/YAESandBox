import {useEraLiteGameMenu} from "#/features/home/useEraLiteGameMenu.ts";
import type {ISaveManager} from "#/share/ISaveUIManager.ts";
import type {SaveSlot} from "@yaesandbox-frontend/core-services/playerSave";

/**
 * 这是 ISaveManager 接口的 Era-Lite 具体实现。
 * 它作为一个适配器，将 useEraLiteGameMenu 提供的通用功能
 * 转换为 useSaveManagerCard UI Composable 所需的特定契约。
 */
export function useEraLiteSaveManager(): ISaveManager
{
    // 1. 获取底层的、包含所有核心业务逻辑的 gameMenu 实例
    const gameMenu = useEraLiteGameMenu();

    // 2. 实现 ISaveManager 接口的每一个部分

    // 查询方法
    function findAutosaveByName(name: string): SaveSlot | null
    {
        return gameMenu.slots.value.find(s => s.type === 'autosave' && s.name === name) || null;
    }

    // 操作方法 (直接映射)
    async function loadAutosave(slotId: string): Promise<void>
    {
        await gameMenu.loadGame(slotId);
    }

    async function loadFromSnapshot(snapshotId: string, newAutosaveName: string): Promise<void>
    {
        await gameMenu.loadFromSnapshot(snapshotId, newAutosaveName);
    }

    async function createAutosave(name: string): Promise<void>
    {
        // startNewGame 已经包含了 "创建并加载" 的逻辑
        await gameMenu.startNewGame(name);
    }

    async function createSnapshot(name: string): Promise<void>
    {
        await gameMenu.createSnapshot(name);
    }

    // 3. 返回一个严格遵守 ISaveManager 契约的对象
    return {
        // 状态 (直接传递)
        slots: gameMenu.slots,
        activeSlot: gameMenu.activeSlot,
        activeSlotId: gameMenu.activeSlotId,

        // 操作
        loadAutosave,
        loadFromSnapshot,
        createAutosave,
        createSnapshot,

        // 查询
        findAutosaveByName,
    };
}