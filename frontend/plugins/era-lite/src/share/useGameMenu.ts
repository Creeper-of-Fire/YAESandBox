import {computed, readonly, ref} from 'vue';
import type {Router} from 'vue-router';
import type {StorageAdapter} from '#/share/services/storageAdapter';
import type {StorageScopeManager} from '#/share/useStorageScopeManager';
import type {useSaveSlotManager} from '#/share/useSaveSlotManager';

// 定义 useGameMenu 需要的依赖
interface GameMenuDependencies {
    storageAdapter: StorageAdapter;
    scopeManager: StorageScopeManager;
    saveSlotManager: ReturnType<typeof useSaveSlotManager>;
    router: Router;
    lastActiveScopeKey: string; // 用于存储最后激活存档的 key
    gameRouteName: string;      // 加载游戏后要跳转的路由名称
}

/**
 * 一个通用的、与具体游戏无关的 Composable，用于处理主菜单逻辑。
 * @param deps - 包含所有依赖项的对象。
 */
export function useGameMenu(deps: GameMenuDependencies) {
    const { storageAdapter, scopeManager, saveSlotManager, router, lastActiveScopeKey, gameRouteName } = deps;

    const isInitialized = ref(false);
    const lastActiveScopeName = ref<string | null>(null);

    const isGameLoaded = computed(() => !!scopeManager.activeScopeName.value);

    async function initialize() {
        if (isInitialized.value) return;

        // 刷新存档槽列表
        await saveSlotManager.refreshSlots();
        const available = scopeManager.availableScopes.value;

        if (available.length > 0) {
            const lastActive = await storageAdapter.getItem<string>([], lastActiveScopeKey);
            if (lastActive && available.includes(lastActive)) {
                lastActiveScopeName.value = lastActive;
            } else if (available.length > 0) {
                // 如果记录无效，则将最新的存档（列表已排序）作为备选
                lastActiveScopeName.value = available[0];
            }
        }
        isInitialized.value = true;
    }

    async function selectScopeAndRecord(scopeName: string | null) {
        await scopeManager.selectScope(scopeName);
        if (scopeName) {
            await storageAdapter.setItem([], lastActiveScopeKey, scopeName);
            lastActiveScopeName.value = scopeName;
        }
    }

    async function loadLastGame() {
        if (lastActiveScopeName.value) {
            await selectScopeAndRecord(lastActiveScopeName.value);
            await router.push({ name: gameRouteName });
        } else {
            console.error("没有可供继续的游戏。");
        }
    }

    async function startNewGame(name: string) {
        const newScopeId = await saveSlotManager.createAutosave(name);
        await selectScopeAndRecord(newScopeId);
        await router.push({ name: gameRouteName });
    }

    async function quitToMainMenu() {
        // 卸载当前存档，这将触发路由守卫
        await scopeManager.selectScope(null);
    }

    // 首次调用时进行初始化
    initialize();

    return {
        isGameLoaded,
        lastActiveScopeName: readonly(lastActiveScopeName),
        isInitialized: readonly(isInitialized),
        loadLastGame,
        startNewGame,
        quitToMainMenu,
    };
}