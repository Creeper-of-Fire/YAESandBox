// src/stores/useEraLiteGlobalScopeStore.ts
import {defineStore} from 'pinia';
import {localforageAdapter, type StorageAdapter} from '#/share/services/storageAdapter'; // or apiStorageAdapter
import {type StorageScopeManager, useStorageScopeManager} from '#/share/useStorageScopeManager.ts';
import {markRaw, readonly, ref} from "vue";
import {createScopedPersistentState} from "#/share/createScopedPersistentState.ts";
import {useSaveSlotManager} from "#/share/useSaveSlotManager.ts";
import {nanoid} from 'nanoid';
// 定义我们游戏存档的根目录
const SAVE_ROOT_PATH = ['saves'];
export const LAST_ACTIVE_SCOPE_KEY = 'era-lite-last-active-scope';

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

    // 将整个 manager 对象返回，这样消费者可以使用其所有功能
    return {
        // Services
        storageAdapter: markRaw(storageAdapter),
        scopeManager: markRaw(scopeManager),
        saveSlotManager: markRaw(saveSlotManager),
        // Utils
        createState
    };
});