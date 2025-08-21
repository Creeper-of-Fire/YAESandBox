import {useRoute} from 'vue-router';
import type {RemovableRef, StorageLike, UseStorageOptions} from '@vueuse/core';
import {useStorage} from '@vueuse/core';
import {getCurrentInstance, inject} from 'vue';
import {PluginUniqueNameKey} from "@yaesandbox-frontend/core-services/injectKeys";

/**
 * 获取当前组件的作用域标识符。
 * 优先使用组件的 'name' 选项。
 * 在开发模式下，如果 name 未提供，则回退到文件路径并发出警告。
 * @returns {string | null} 组件作用域标识符或 null。
 */
function getComponentScope(): string | null
{
    const instance = getCurrentInstance();
    if (!instance)
    {
        if ((import.meta as any).env.DEV)
        {
            console.warn('[useScopedStorage] 无法在组件 setup 上下文之外获取实例。');
        }
        return null;
    }

    const componentName = instance.type.name;
    if (componentName)
    {
        return componentName;
    }

    // --- 仅限开发模式的辅助功能 ---
    if ((import.meta as any).env.DEV)
    {
        const filePath = (instance.type as any).__file;
        if (filePath)
        {
            console.warn(
                `[useScopedStorage] 组件 (${filePath}) 未设置 'name' 选项。` +
                `建议使用 defineOptions({ name: '...' }) 来创建一个稳定的存储 key。` +
                `当前已回退到使用文件路径作为 key。`
            );
            // 从文件路径中提取一个相对干净的 key
            // e.g., /src/views/ShopView.vue -> ShopView
            const key = filePath.split('/').pop()?.replace('.vue', '');
            return key || filePath;
        }
    }

    return null;
}

/**
 * 一个带有自动作用域的 useStorage 封装。
 * 它会根据调用它的上下文（组件或路由）自动生成唯一的 key，
 * 以避免在不同组件或视图中发生 key 冲突。
 *
 * 作用域优先级: 组件 name > (开发模式下的组件文件路径) > 路由 name/path
 *
 * @param localKey - 在当前作用域内的局部 key。
 * @param initialValue - 初始值。
 * @param storage - (可选) 存储引擎，默认使用 localStorage。
 * @param options - (可选) 传递给 useStorage 的选项。
 * @returns RemovableRef<T>
 */
export function useScopedStorage<T>(
    localKey: string,
    initialValue: T,
    storage?: StorageLike | undefined,
    options?: UseStorageOptions<T>
): RemovableRef<T>
{
    const route = useRoute();

    // 1. 尝试注入插件运行时 ID (最高优先级)
    const pluginId = inject(PluginUniqueNameKey, null);

    // 2. 尝试获取组件作用域
    const componentScope = getComponentScope();

    // 3. 回退到路由作用域
    const routeScope = (route.name?.toString() || route.path).replace(/\//g, '_');

    // 组合全局 Key，优先级: 插件 > 组件 > 路由
    const scopeParts = [
        pluginId,
        componentScope,
        // 如果没有更具体的作用域，才使用路由
        (!pluginId && !componentScope) ? routeScope : null
    ].filter(Boolean); // 过滤掉 null 或 undefined 的部分

    const scope = scopeParts.join(':') || 'global'; // 如果都为空，则有个默认值

    const globalKey = `storage:${scope}:${localKey}`;

    return useStorage(globalKey, initialValue, storage, options);
}