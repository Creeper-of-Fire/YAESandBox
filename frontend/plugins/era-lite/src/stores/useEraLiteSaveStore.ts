// src/stores/useEraLiteGlobalScopeStore.ts
import {defineStore} from 'pinia';
import {localforageAdapter, type StorageAdapter} from '#/share/services/storageAdapter'; // or apiStorageAdapter
import {type StorageScopeManager, useStorageScopeManager} from '#/share/useStorageScopeManager.ts';
import {markRaw, readonly, ref} from "vue";
import {createScopedPersistentState} from "#/share/createScopedPersistentState.ts";
import {useSaveSlotManager} from "#/share/useSaveSlotManager.ts";

// 定义我们游戏存档的根目录
const SAVE_ROOT_PATH = ['saves'];
const LAST_ACTIVE_SCOPE_KEY = 'era-lite-last-active-scope';

export const useEraLiteSaveStore = defineStore('era-lite-global-scope', () =>
{
    const storageAdapter: StorageAdapter = localforageAdapter;

    /**
     * 这是本 Store 的核心：它初始化一个作用域管理器，并将其作为一个
     * 全局可访问的、响应式的服务提供给应用的其他部分。
     *
     * 所有关于作用域的【状态和逻辑】都封装在 useStorageScopeManager 中，
     * 本 Store 只负责【实例化和提供】它。
     */
    const scopeManager: StorageScopeManager = useStorageScopeManager(
        storageAdapter, // 在这里注入我们选择的存储后端
        SAVE_ROOT_PATH
    );

    const isInitialized = ref(false);

    /**
     * 应用启动时的核心初始化逻辑。
     * 负责加载存档列表，并决定激活哪个存档。
     */
    async function initialize()
    {
        if (isInitialized.value) return;

        // 1. 从存储刷新作用域列表
        await scopeManager.refreshScopes();
        const available = scopeManager.availableScopes.value;

        let scopeToSelect: string | null = null;

        if (available.length > 0)
        {
            // 2. 如果有存档，尝试加载上次激活的存档
            const lastActive = await storageAdapter.getItem<string>([], LAST_ACTIVE_SCOPE_KEY);
            if (lastActive && available.includes(lastActive))
            {
                scopeToSelect = lastActive;
            }
            else
            {
                // 如果没有记录或记录无效，则默认选择第一个
                scopeToSelect = available[0];
            }
        }
        else
        {
            // 3. 如果没有任何存档，则创建并选择一个全新的存档
            console.log("未找到任何存档，正在创建新的默认存档...");
            const newScopeName = await scopeManager.createScope("默认存档");

            // 【关键】在这里写入初始 meta.json 数据
            const initialMeta = {name: "默认存档", createdAt: new Date().toISOString()};
            // 注意：因为 createScopedPersistentState 还没"准备好"，我们直接用底层 adapter
            await storageAdapter.setItem([...SAVE_ROOT_PATH, newScopeName], 'meta.json', initialMeta);

            scopeToSelect = newScopeName;
        }

        // 4. 最终选择一个作用域
        await selectScope(scopeToSelect);
        isInitialized.value = true;
        console.log("存档系统初始化完成。");
    }

    /**
     * 包装了 scopeManager.selectScope，增加了持久化逻辑。
     * 这是应用中切换存档应该调用的唯一方法。
     */
    async function selectScope(scopeName: string | null)
    {
        await scopeManager.selectScope(scopeName);
        if (scopeName)
        {
            await storageAdapter.setItem([], LAST_ACTIVE_SCOPE_KEY, scopeName);
        }
        else
        {
            await storageAdapter.removeItem([], LAST_ACTIVE_SCOPE_KEY);
        }
    }

    /**
     * 创建一个预配置的、作用域感知的持久化状态。
     * 这个函数是 createScopedPersistentState 的一个便捷封装，
     * 它自动注入了本 Store 提供的全局 storageAdapter 和 scopeManager。
     *
     * @param fileName - 要绑定的文件名
     * @param initialState - 初始状态
     * @returns 返回一个包含 state ref 和 isReady ref 的对象。
     */
    function createState<T>(fileName: string, initialState: T)
    {
        // 在内部调用我们通用的、解耦的 Composable
        return createScopedPersistentState<T>(fileName, {
            initialState,
            storageAdapter: storageAdapter, // 自动注入
            scopeManager: scopeManager,     // 自动注入
        });
    }

    const saveSlotManager = useSaveSlotManager(
        scopeManager,
        storageAdapter
    );

    initialize();

    // 将整个 manager 对象返回，这样消费者可以使用其所有功能
    return {
        storageAdapter,
        scopeManager: markRaw(scopeManager),
        saveSlotManager: markRaw(saveSlotManager),
        createState,
        selectScope,
        isInitialized: readonly(isInitialized),
    };
});