import {computed, readonly, ref} from 'vue';
import {nanoid} from 'nanoid';
import type {StorageScopeManager} from './useStorageScopeManager.ts';
import type {StorageAdapter} from '#/share/services/storageAdapter.ts';

/** 描述一个存档槽在UI上的表现 */
export interface SaveSlot
{
    id: string; // 目录名
    name: string;
    type: 'autosave' | 'snapshot';
    createdAt: number;
}

/** 描述存储在每个存档目录中 meta.json 文件的结构 */
export interface SaveSlotMeta
{
    name: string;
    type: 'autosave' | 'snapshot';
    createdAt: number;
}

const META_FILENAME = 'meta.json';

export function useSaveSlotManager(
    scopeManager: StorageScopeManager,
    storageAdapter: StorageAdapter
)
{
    const slots = ref<SaveSlot[]>([]);
    const isInitialized = ref(false);

    // activeSlotId 直接代理 scopeManager 的当前作用域
    const activeSlotId = computed(() => scopeManager.activeScopeName.value);

    /**
     * 根据名称查找一个自动存档。
     * @param name 要查找的自动存档名称。
     * @returns 找到的 SaveSlot 对象，或 null。
     */
    function findAutosaveByName(name: string): SaveSlot | null
    {
        return slots.value.find(s => s.type === 'autosave' && s.name === name) || null;
    }

    /**
     * 直接切换到指定的自动存档ID。
     * @param autosaveId 必须是一个自动存档的ID。
     */
    async function selectAutosave(autosaveId: string): Promise<void>
    {
        await scopeManager.selectScope(autosaveId);
    }

    /**
     * 从快照创建一个新的自动存档并切换过去。
     * @param snapshotId 快照的ID。
     * @param newAutosaveName 新自动存档的名称。
     * @returns 新创建的自动存档ID。
     */
    async function loadFromSnapshot(snapshotId: string, newAutosaveName: string): Promise<string>
    {
        const newAutosaveId = `autosave_from_snapshot_${nanoid(8)}`;

        // 1. 使用 scopeManager 进行底层复制
        await scopeManager.copyScope(snapshotId, newAutosaveId);

        // 2. 覆盖/创建新的元数据，定义这个新存档的身份
        const newMeta: SaveSlotMeta = {name: newAutosaveName, type: 'autosave', createdAt: Date.now()};
        const newPath = [...scopeManager.rootPath.value, newAutosaveId];
        await storageAdapter.setItem(newPath, META_FILENAME, newMeta);

        // 3. 切换到新创建的自动存档
        await scopeManager.selectScope(newAutosaveId);
        await refreshSlots();
        return newAutosaveId;
    }

    /** 刷新UI显示的存档列表 */
    async function refreshSlots()
    {
        await scopeManager.refreshScopes();
        const availableScopeIds = scopeManager.availableScopes.value;
        const rootPath = scopeManager.rootPath.value;

        // 1. 立即用占位符更新UI，让用户看到即时反馈
        const placeholderSlots: SaveSlot[] = availableScopeIds.map(id => ({
            id,
            name: '加载中...',
            type: 'autosave', // 默认值
            createdAt: 0,
            loading: true,
        }));
        slots.value = placeholderSlots;

        // 2. 异步地、逐个地获取每个存档的元数据来“填充”占位符
        for (const placeholder of placeholderSlots)
        {
            try
            {
                const meta = await storageAdapter.getItem<SaveSlotMeta>(
                    [...rootPath, placeholder.id],
                    META_FILENAME
                );

                const targetSlot = slots.value.find(s => s.id === placeholder.id);
                if (targetSlot && meta)
                {
                    // 找到对应的槽并更新它，这将触发单个列表项的响应式更新
                    targetSlot.name = meta.name;
                    targetSlot.type = meta.type;
                    targetSlot.createdAt = meta.createdAt;
                }
            } catch (e)
            {
                console.error(`Failed to load meta for slot ${placeholder.id}`, e);
                const targetSlot = slots.value.find(s => s.id === placeholder.id);
                if (targetSlot)
                {
                    targetSlot.name = '读取失败';
                }
            }
        }

        // 3. （可选）加载完后按时间排序
        slots.value.sort((a, b) => b.createdAt - a.createdAt);
    }

    /** "开启新分支" -> 创建一个新的自动存档 */
    async function createAutosave(name: string): Promise<string>
    {
        const id = `autosave_${nanoid(8)}`;
        const newPath = [...scopeManager.rootPath.value, id];

        const newMeta: SaveSlotMeta = {name, type: 'autosave', createdAt: Date.now()};
        await storageAdapter.setItem(newPath, META_FILENAME, newMeta);

        await scopeManager.createScope(id); // createScope 仍用于创建目录和切换
        await refreshSlots();
        return id;
    }

    /** 基于当前自动存档创建快照 */
    async function createSnapshot(name: string): Promise<string>
    {
        const sourceId = activeSlotId.value;
        if (!sourceId)
        {
            console.error("Cannot create snapshot, no active slot selected.");
            return '';
        }

        const snapshotId = `snapshot_${nanoid(8)}`;

        // 1. 复制内容
        await scopeManager.copyScope(sourceId, snapshotId);

        // 2. 更新元数据
        const newPath = [...scopeManager.rootPath.value, snapshotId];
        const newMeta: SaveSlotMeta = {name, type: 'snapshot', createdAt: Date.now()};
        await storageAdapter.setItem(newPath, META_FILENAME, newMeta);

        // 3. 刷新UI
        await refreshSlots();
        return snapshotId;
    }

    // --- 初始化 ---
    async function initialize()
    {
        if (isInitialized.value) return;
        await refreshSlots();
        isInitialized.value = true;
    }

    initialize();

    return {
        slots: readonly(slots),
        activeSlotId,
        createAutosave,
        createSnapshot,
        refreshSlots,
        findAutosaveByName,
        selectAutosave,
        loadFromSnapshot,
    };
}