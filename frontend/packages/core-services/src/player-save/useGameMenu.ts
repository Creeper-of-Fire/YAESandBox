import {computed, readonly, ref} from 'vue';
import type {IProjectMetaStorage} from "./storage/IProjectMetaStorage.ts";
import type {ISaveSlotManager, SaveSlot} from "./storage/ISaveSlotManager.ts";

// 定义 useGameMenu 需要的依赖项接口
export interface GameMenuDependencies
{
    saveSlotManager: ISaveSlotManager;
    projectMetaStorage: IProjectMetaStorage;
}

// 用于在后端项目元数据中存储最后激活存档ID的 Key
const LAST_ACTIVE_SLOT_ID_KEY = 'lastActiveSlotId';

/**
 * 一个通用的、与具体应用无关的 Composable，用于处理游戏菜单的核心逻辑。
 * 它负责管理存档槽状态、与后端服务交互，并向上层提供响应式数据和操作方法。
 * @param deps - 包含所有已配置好的服务实例的对象。
 */
export function useGameMenu(deps: GameMenuDependencies)
{
    const {saveSlotManager, projectMetaStorage} = deps;

    // --- 内部状态 ---
    const slots = ref<SaveSlot[]>([]);
    const activeSlotId = ref<string | null>(null);
    const lastActiveSlotId = ref<string | null>(null); // 从后端加载的“最后存档”记录
    const isInitialized = ref(false);
    const isLoading = ref(false);

    // --- 派生状态 (Computed) ---
    const isGameLoaded = computed(() => !!activeSlotId.value);
    const activeSlot = computed(() => slots.value.find(s => s.id === activeSlotId.value) || null);
    const lastActiveSlot = computed(() =>
    {
        const lastId = lastActiveSlotId.value;
        return lastId ? slots.value.find(s => s.id === lastId) || null : null;
    });
    const lastActiveSlotName = computed(() => lastActiveSlot.value?.name)

    // --- 内部核心方法 ---

    /** 刷新存档列表 */
    async function refreshSlots()
    {
        isLoading.value = true;
        try
        {
            const fetchedSlots = await saveSlotManager.listSlots();
            // 按创建时间降序排序
            slots.value = fetchedSlots.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
        } catch (error)
        {
            console.error("Failed to refresh save slots:", error);
            slots.value = [];
        } finally
        {
            isLoading.value = false;
        }
    }

    /**
     * 设置当前激活的存档，并将其持久化到后端。
     * @param slotId - 要激活的存档ID，或 null 表示退出游戏。
     */
    async function _setActiveSlot(slotId: string | null)
    {
        activeSlotId.value = slotId;
        await projectMetaStorage.setItem(LAST_ACTIVE_SLOT_ID_KEY, slotId);
        // 如果我们正在加载一个新游戏，那么它也成为了“最后激活”的游戏
        if (slotId)
        {
            lastActiveSlotId.value = slotId;
        }
    }


    // --- 公开的操作 (Actions) ---

    /**
     * 加载指定的存档。
     * @param slotId - 要加载的存档ID。
     */
    async function loadGame(slotId: string)
    {
        if (slots.value.some(s => s.id === slotId))
        {
            await _setActiveSlot(slotId);
        }
        else
        {
            console.error(`Attempted to load a non-existent slot: ${slotId}`);
        }
    }

    /** 加载最后玩过的游戏 */
    async function loadLastGame()
    {
        if (lastActiveSlot.value)
        {
            await loadGame(lastActiveSlot.value.id);
        }
        else
        {
            console.error("No last active game to continue.");
        }
    }

    /**
     * 开始一个新游戏（创建一个新的自动存档并加载它）。
     * @param name - 新游戏的名称。
     * @returns 创建成功后的新存档槽。
     */
    async function startNewGame(name: string): Promise<SaveSlot>
    {
        isLoading.value = true;
        try
        {
            const newSlot = await saveSlotManager.createSlot(name, 'autosave');
            slots.value.unshift(newSlot); // 添加到列表顶部
            await _setActiveSlot(newSlot.id);
            return newSlot;
        } finally
        {
            isLoading.value = false;
        }
    }

    /** 退出当前游戏返回主菜单 */
    async function quitToMainMenu()
    {
        await _setActiveSlot(null);
    }

    /**
     * 基于当前激活的存档创建一个快照。
     * @param name - 快照的名称。
     */
    async function createSnapshot(name: string)
    {
        if (!activeSlot.value)
        {
            throw new Error("Cannot create snapshot: No game is currently loaded.");
        }
        isLoading.value = true;
        try
        {
            const newSnapshot = await saveSlotManager.copySlot(activeSlot.value.id, name, 'snapshot');
            slots.value.unshift(newSnapshot); // 添加到列表顶部
        } finally
        {
            isLoading.value = false;
        }
    }

    /**
     * 从一个快照创建一个新的自动存档分支，并立即加载它。
     * @param snapshotId - 源快照的 ID。
     * @param newAutosaveName - 新自动存档分支的名称。
     */
    async function loadFromSnapshot(snapshotId: string, newAutosaveName: string)
    {
        isLoading.value = true;
        try
        {
            // 1. 调用底层服务复制存档
            const newSlot = await saveSlotManager.copySlot(snapshotId, newAutosaveName, 'autosave');
            // 2. 使用乐观更新，直接将新存档添加到本地列表
            slots.value.unshift(newSlot);

            await _setActiveSlot(newSlot.id);
        } finally
        {
            isLoading.value = false;
        }
    }

    // --- 初始化逻辑 ---
    async function initialize()
    {
        if (isInitialized.value) return;
        isLoading.value = true;
        try
        {
            await refreshSlots();
            const lastId = await projectMetaStorage.getItem<string>(LAST_ACTIVE_SLOT_ID_KEY);
            // 验证从后端获取的 lastId 是否仍然有效
            if (lastId && slots.value.some(s => s.id === lastId))
            {
                lastActiveSlotId.value = lastId;
            }
            else
            {
                lastActiveSlotId.value = null;
            }
        } finally
        {
            isLoading.value = false;
            isInitialized.value = true;
        }
    }

    initialize();

    return {
        // --- 状态 ---
        isInitialized: readonly(isInitialized),
        isLoading: readonly(isLoading),
        slots: readonly(slots),
        activeSlot: readonly(activeSlot),
        activeSlotId: readonly(activeSlotId),
        lastActiveSlot: readonly(lastActiveSlot),
        lastActiveSlotName: readonly(lastActiveSlotName),
        isGameLoaded: readonly(isGameLoaded),

        // --- 操作 ---
        initialize,
        refreshSlots,
        loadGame,
        loadLastGame,
        loadFromSnapshot,
        startNewGame,
        quitToMainMenu,
        createSnapshot,
    };
}