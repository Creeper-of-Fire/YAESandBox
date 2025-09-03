import type {Ref} from 'vue';
import {ref, toRaw, watch} from 'vue';
import type {StorageScopeManager} from "#/share/useStorageScopeManager";
import type {StorageAdapter} from "#/share/services/storageAdapter";

interface ScopedPersistentStateOptions<T>
{
    /** 初始状态，如果存储中没有值，则使用此值。 */
    initialState: T;
    /** 底层存储适配器。 */
    storageAdapter: StorageAdapter;
    /** 全局作用域管理器，提供响应式的当前路径。 */
    scopeManager: StorageScopeManager;
    /** 是否深度监听状态变化。 */
    deep?: boolean;
}

/**
 * 创建一个与当前激活作用域中的特定文件双向绑定的响应式状态 (ref)。
 *
 * 当作用域切换时，它会自动从新路径加载数据。
 *
 * @param fileName - 在当前作用-域中要绑定的文件名 (例如 'characters.json')。
 * @param options - 配置对象，包含初始状态、存储适配器和作用域管理器。
 * @returns 返回一个包含 state ref 和 isReady ref 的对象。
 */
export function createScopedPersistentState<T>(
    fileName: string,
    options: ScopedPersistentStateOptions<T>
)
{
    const {initialState, storageAdapter, scopeManager, deep = true} = options;

    const state: Ref<T> = ref(JSON.parse(JSON.stringify(initialState))) as Ref<T>;

    // 表示“状态是否已与一个【具体的存档文件】同步”
    const isReady = ref(false);

    // --- 核心加载逻辑 ---
    async function load(path: readonly string[])
    {
        isReady.value = false;
        try
        {
            const storedValue = await storageAdapter.getItem<T>(path, fileName);
            // 只有当存储中有值时才覆盖，否则保持 initialState
            state.value = storedValue !== null && storedValue !== undefined
                ? storedValue
                : JSON.parse(JSON.stringify(initialState)); // 使用深拷贝避免污染
        } catch (error)
        {
            console.error(`[createScopedPersistentState] 加载 [${[...path, fileName].join('/')}] 时失败:`, error);
            // 加载失败时也恢复到初始状态
            state.value = JSON.parse(JSON.stringify(initialState));
        } finally
        {
            // 无论成功与否，加载过程都已完成
            isReady.value = true;
        }
    }

    // --- 核心保存逻辑 ---
    async function save(path: readonly string[], value: T)
    {
        // 只有在数据加载完成后才允许保存，防止初始状态意外覆盖已有存档。
        if (!isReady.value) return;

        try
        {
            await storageAdapter.setItem(path, fileName, toRaw(value));
        } catch (error)
        {
            console.error(`[createScopedPersistentState] Failed to save [${[...path, fileName].join('/')}]:`, error);
        }
    }

    /**
     * 重置状态到初始值，并标记为“未就绪”。
     * 在退出到主菜单或存档加载失败时调用。
     */
    function resetToInitial() {
        state.value = JSON.parse(JSON.stringify(initialState));
        isReady.value = false;
    }

    // --- 响应式绑定 ---

    // 1. 监听作用域路径的变化。当路径改变时（切换存档），重新加载数据。
    watch(
        () => scopeManager.activeScopePath.value,
        (newPath) =>
        {
            if (newPath) {
                // 如果有新路径（加载存档），则加载数据
                load(newPath);
            } else {
                // 如果路径为 null（退出到主菜单），则重置状态
                console.log(`Scope unloaded. Resetting state for "${fileName}".`);
                resetToInitial();
            }
        },
        {immediate: true, deep: true} // immediate: 立即加载初始作用域的数据
    );

    // 2. 监听本地 state 的变化。当 state 改变时，将其保存回当前作用域路径。
    watch(
        state,
        (newValue) =>
        {
            const currentPath = scopeManager.activeScopePath.value;
            // 只有在路径存在时才保存
            if (currentPath) {
                save(currentPath, newValue);
            }
        },
        {deep}
    );

    return {state, isReady};
}