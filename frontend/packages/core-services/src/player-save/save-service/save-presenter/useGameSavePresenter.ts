import type {SaveSlot} from "../../storage/ISaveSlotManager.ts";
import {useGameSaveService} from "../injectKeys.ts";
import type {IGameSavePresenter} from "./IGameSavePresenter.ts";


/**
 * 这是 ISaveManager 接口的具体实现。
 * 它作为一个适配器，将 useGameSaveService 提供的通用功能
 * 转换为 useGameSavePresenterUI 所需的特定契约。
 */
export function useGameSavePresenter(): IGameSavePresenter
{
    // 1. 获取底层的、包含所有核心业务逻辑的 saveService 实例
    const saveService = useGameSaveService();

    // 2. 实现 ISaveManager 接口的每一个部分
    // 查询方法
    function findAutosaveByName(name: string): SaveSlot | null
    {
        return saveService.slots.value.find(s => s.type === 'autosave' && s.name === name) || null;
    }

    // 操作方法 (直接映射)
    async function loadAutosave(slotId: string): Promise<void>
    {
        await saveService.loadGame(slotId);
    }

    async function loadFromSnapshot(snapshotId: string, newAutosaveName: string): Promise<void>
    {
        await saveService.loadFromSnapshot(snapshotId, newAutosaveName);
    }

    async function createAutosave(name: string): Promise<void>
    {
        // startNewGame 已经包含了 "创建并加载" 的逻辑
        await saveService.startNewGame(name);
    }

    async function createSnapshot(name: string): Promise<void>
    {
        await saveService.createSnapshot(name);
    }

    // 3. 返回一个严格遵守 ISaveManager 契约的对象
    return {
        // 状态 (直接传递)
        slots: saveService.slots,
        activeSlot: saveService.activeSlot,
        activeSlotId: saveService.activeSlotId,

        // 操作
        loadAutosave,
        loadFromSnapshot,
        createAutosave,
        createSnapshot,

        // 查询
        findAutosaveByName,
    };
}