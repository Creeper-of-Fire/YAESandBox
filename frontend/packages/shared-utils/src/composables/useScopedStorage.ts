import {useRoute} from 'vue-router';
import {type RemovableRef, type StorageLike, useStorage, type UseStorageOptions} from '@vueuse/core';

/**
 * 一个带有路由作用域的 useStorage 封装。
 * 自动将当前路由的名称或路径作为 key 的前缀，
 * 以避免在不同视图组件中发生 key 冲突。
 *
 * @param localKey - 在当前组件/视图内的局部 key。
 * @param initialValue - 初始值。
 * @param instanceKey - (可选) 父组件传入的唯一标识符，用于区分同一视图内的多个实例。
 * @param storage - (可选) 存储引擎，默认使用 localStorage。
 * @param options - (可选) 传递给 useStorage 的选项。
 * @returns RemovableRef<T>
 */
export function useScopedStorage<T>(
    localKey: string,
    initialValue: T,
    instanceKey?: string,
    storage?: StorageLike | undefined,
    options?: UseStorageOptions<T>
): RemovableRef<T>
{
    const route = useRoute();

    // 优先使用路由的 name，因为它通常更干净。如果没有，则回退到 path。
    // 将斜杠替换为下划线，创建一个更安全的 key
    const routeScope = (route.name?.toString() || route.path).replace(/\//g, '_');

    // 组合成一个全局唯一的 key
    const globalKey = instanceKey
        ? `scoped-storage:${routeScope}:${instanceKey}:${localKey}` // e.g., scoped-storage:Shop:products:filter-state
        : `scoped-storage:${routeScope}:${localKey}`;

    return useStorage(globalKey, initialValue, storage, options);
}