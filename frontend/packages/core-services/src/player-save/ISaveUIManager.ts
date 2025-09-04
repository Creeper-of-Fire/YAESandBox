import type {Ref} from "vue";
import type {SaveSlot} from "./storage/ISaveSlotManager.ts";

/**
 * 为 SaveManagerCard UI 组件提供所需全部状态和行为的接口。
 * 这个接口精确地定义了 UI 与存档业务逻辑之间的契约。
 */
export interface ISaveManager
{
    // --- 响应式状态 ---
    slots: Readonly<Ref<readonly SaveSlot[]>>;
    activeSlot: Readonly<Ref<SaveSlot | null>>;
    activeSlotId: Readonly<Ref<string | null>>;

    // --- 核心操作 ---

    /** 直接加载一个自动存档 */
    loadAutosave(slotId: string): Promise<void>;

    /** 从一个快照创建一个新的自动存档分支，并加载它 */
    loadFromSnapshot(snapshotId: string, newAutosaveName: string): Promise<void>;

    /** 创建一个全新的自动存档分支，并加载它 */
    createAutosave(name: string): Promise<void>;

    /** 基于当前激活的存档创建快照 */
    createSnapshot(name: string): Promise<void>;

    // --- 查询方法 ---

    /** 根据名称查找一个自动存档 */
    findAutosaveByName(name: string): SaveSlot | null;
}