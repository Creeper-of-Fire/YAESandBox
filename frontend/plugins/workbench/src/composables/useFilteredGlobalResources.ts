// src/composables/useFilteredGlobalResources.ts
import {computed, ref} from 'vue';
import type {ConfigType} from '#/services/GlobalEditSession';
import {useGlobalResources} from '#/composables/useGlobalResources';
import type {ConfigTypeMap} from '#/stores/workbenchStore';

/**
 * 一个通用的、响应式的 Composable，用于获取并根据标签筛选全局资源列表。
 * 它包装了 `useGlobalResources`，在其之上添加了筛选和标签聚合的功能。
 *
 * @param type - 要获取的资源类型 ('workflow', 'tuum', 'rune')
 * @returns 返回一个对象，包含筛选后的资源、所有可用标签、加载状态和错误状态。
 */
export function useFilteredGlobalResources<K extends ConfigType>(type: K)
{
    type T = ConfigTypeMap[K];

    // 1. 在 Composable 内部创建并管理筛选状态
    const filterTags = ref<string[]>([]);

    // 2. 调用底层的 Composable 获取原始的、未筛选的数据
    const {resources: allResources, isLoading, error, execute} = useGlobalResources(type);

    // 3. 核心：创建筛选后的资源计算属性
    const filteredResources = computed(() =>
    {
        const baseResources = allResources.value;
        const tagsToFilter = filterTags.value;

        // 如果没有筛选标签，直接返回所有资源，避免不必要的计算
        if (!tagsToFilter || tagsToFilter.length === 0)
        {
            return baseResources;
        }

        // 如果有筛选标签，则进行过滤
        const filteredEntries = Object.entries(baseResources).filter(([, item]) =>
        {
            // 只筛选成功的项
            if (!item.isSuccess)
            {
                return false;
            }
            const itemTags = item.meta?.tags;
            // 如果资源项没有标签，则不匹配
            if (!itemTags || itemTags.length === 0)
            {
                return false;
            }
            // 检查资源项的标签是否 **包含所有** 筛选标签 (AND 逻辑)
            return tagsToFilter.every(tag => itemTags.includes(tag));
        });

        // 将筛选后的条目转换回 Record 对象
        return Object.fromEntries(filteredEntries);
    });

    // 3. 聚合所有可用标签，用于筛选器选项
    const allAvailableTags = computed<string[]>(() =>
    {
        const tagSet = new Set<string>();
        for (const key in allResources.value)
        {
            const item = allResources.value[key];
            if (item.isSuccess && item.meta?.tags)
            {
                item.meta.tags.forEach(tag => tagSet.add(tag));
            }
        }
        // 排序以保证 UI 显示稳定
        return Array.from(tagSet).sort();
    });

    // 4. 将最终的视图和状态暴露给组件
    return {
        /**
         * 经过标签筛选后的资源列表 (Record<storeId, GlobalResourceItem>)。
         */
        resources: filteredResources,
        /**
         * 从所有资源中提取出的、不重复的、已排序的标签列表。
         */
        allAvailableTags,
        /**
         * 当前选中的筛选标签。可直接用于 v-model。
         */
        filterTags,
        isLoading,
        error,
        execute,
    };
}