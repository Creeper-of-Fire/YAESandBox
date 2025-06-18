// --- START OF FILE frontend/src/app-workbench/composables/useAsyncResource.ts ---

import {ref, type Ref, shallowRef} from 'vue';

interface AsyncResource<T> {
    data: Ref<T>;
    isLoading: Ref<boolean>;
    error: Ref<any>;
    /**
     * 加载或重新加载资源。
     * @param force - 是否强制重新加载，忽略缓存。
     * @returns {Promise<void>}
     */
    load: (force?: boolean) => Promise<void>;
}

/**
 * 创建一个按需加载的、带缓存的响应式资源。
 * @param loader - 一个返回 Promise 的函数，用于加载实际数据。
 * @param initialValue - 数据的初始值。
 */
export function useAsyncResource<T>(loader: () => Promise<T>, initialValue: T): AsyncResource<T> {
    const data = shallowRef<T>(initialValue); // 使用 shallowRef 优化性能
    const isLoading = ref(false);
    const error = ref<any>(null);
    const hasLoaded = ref(false);

    let pendingPromise: Promise<void> | null = null;

    const load = async (force = false): Promise<void> => {
        // 如果已经加载过且不强制刷新，则直接返回
        if (hasLoaded.value && !force) {
            return;
        }

        // 如果当前正在加载中，则等待现有 Promise 完成，避免并发调用
        if (pendingPromise) {
            return pendingPromise;
        }

        isLoading.value = true;
        error.value = null;

        pendingPromise = (async () => {
            try {
                data.value = await loader();
                hasLoaded.value = true;
            } catch (e) {
                error.value = e;
                console.error('Failed to load resource:', e);
            } finally {
                isLoading.value = false;
                pendingPromise = null;
            }
        })();

        return pendingPromise;
    };

    return {
        data,
        isLoading,
        error,
        load,
    };
}
// --- END OF FILE ---