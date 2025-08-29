import { ref, watch, toRaw } from 'vue';
import type { Ref } from 'vue';
import { localforageAdapter, type StorageAdapter } from '#/services/storageAdapter';

interface PersistentStateOptions {
    storage?: StorageAdapter;
    deep?: boolean;
}

/**
 * 创建一个与持久化存储双向绑定的响应式状态 (ref)。
 *
 * @param key - 存储中唯一的键。
 * @param initialState - 如果存储中没有值，则使用的初始状态。
 * @param options - 配置选项，如自定义存储适配器。
 * @returns 返回一个包含 state ref 和 isReady ref 的对象。
 */
export function createPersistentState<T>(
    key: string,
    initialState: T,
    options?: PersistentStateOptions
) {
    const { storage = localforageAdapter, deep = true } = options ?? {};

    const state: Ref<T> = ref(initialState) as Ref<T>;
    const isReady = ref(false); // 标记是否已从存储中加载完成

    // 异步从存储中加载初始数据
    async function load() {
        try {
            const storedValue = await storage.getItem<T>(key);
            if (storedValue !== null && storedValue !== undefined) {
                state.value = storedValue;
            }
        } catch (error) {
            console.error(`[createPersistentState] Failed to load data for key "${key}":`, error);
        } finally {
            isReady.value = true;
        }
    }

    // 立即开始加载
    load();

    // 监听状态变化，并将其写回存储
    watch(state, (newValue) => {
        // 只有在初始数据加载完成后才开始持久化，避免将 initialState 覆盖回存储
        if (isReady.value) {
            storage.setItem(key, toRaw(newValue)).catch(error => {
                console.error(`[createPersistentState] Failed to save data for key "${key}":`, error);
            });
        }
    }, { deep });

    return { state, isReady };
}