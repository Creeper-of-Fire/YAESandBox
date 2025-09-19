import {computed, type Ref} from 'vue';
import {useWorkflowSelector} from './useWorkflowSelector';

/**
 * 定义了用于筛选工作流的条件。
 */
export interface WorkflowFilter
{
    /**
     * 期望工作流必须拥有的输入参数。
     * 这将用于智能排序，将最匹配的排在前面。
     */
    expectedInputs?: string[];

    /**
     * 工作流必须拥有的标签。
     */
    requiredTags?: string[];
}

/**
 * 一个包装器 Composable，在 useWorkflowSelector 的基础上增加了强大的筛选和排序逻辑。
 * 它遵循包装器模式，将展示逻辑与核心数据逻辑分离。
 *
 * @param storageKey 用于持久化用户选择的唯一键。
 * @param filter 一个响应式的筛选条件对象 Ref。
 */
export function useFilteredWorkflowSelector(storageKey: string, filter: Ref<WorkflowFilter>)
{

    // 1. 内部实例化核心 Composable，获取原始数据和基础操作
    const selector = useWorkflowSelector(storageKey);

    // 2. 核心：创建派生的计算属性，用于筛选和排序
    const filteredAndSortedWorkflows = computed(() =>
    {
        if (!selector.availableWorkflows.value) return [];

        const expectedInputs = new Set(filter.value.expectedInputs || []);
        const requiredTags = new Set(filter.value.requiredTags || []);

        const enhanced = selector.availableWorkflows.value
            // --- 步骤 A: 过滤 ---
            .filter(workflow =>
            {
                if (!workflow.resource) return false;

                // 过滤条件1: 必须包含所有 requiredTags
                if (requiredTags.size > 0)
                {
                    const workflowTags = new Set(workflow.resource.tags || []);
                    for (const requiredTag of requiredTags)
                    {
                        if (!workflowTags.has(requiredTag))
                        {
                            return false; // 如果缺少任何一个必需标签，则排除
                        }
                    }
                }

                // 未来可以在这里添加更多过滤逻辑...

                return true;
            })
            // --- 步骤 B: 增强与评分 ---
            .map(workflow =>
            {
                const wfInputs = new Set(workflow.resource.workflowInputs || []);

                // 计算输入匹配度
                const matchedInputs = new Set([...expectedInputs].filter(x => wfInputs.has(x)));
                const missingInputsCount = expectedInputs.size - matchedInputs.size;
                const extraInputsCount = wfInputs.size - matchedInputs.size;

                return {
                    ...workflow,
                    // 评分用于排序
                    score: {
                        missingInputs: missingInputsCount,
                        extraInputs: extraInputsCount,
                    },
                    // 附加信息用于UI展示
                    matchDetails: {
                        matchedInputs: Array.from(matchedInputs),
                        missingInputs: Array.from(expectedInputs).filter(x => !wfInputs.has(x)),
                        extraInputs: Array.from(wfInputs).filter(x => !expectedInputs.has(x)),
                    }
                };
            })
            // --- 步骤 C: 排序 ---
            .sort((a, b) =>
            {
                // 排序规则1: 缺少输入越少的越靠前 (升序)
                if (a.score.missingInputs !== b.score.missingInputs)
                {
                    return a.score.missingInputs - b.score.missingInputs;
                }
                // 排序规则2: 额外输入越少的越靠前 (升序)
                if (a.score.extraInputs !== b.score.extraInputs)
                {
                    return a.score.extraInputs - b.score.extraInputs;
                }
                // 排序规则3: 按名称字母排序作为备用
                return a.resource.name.localeCompare(b.resource.name);
            });

        return enhanced;
    });

    // 3. 返回一个组合了核心功能和增强功能的新对象
    return {
        // --- 从 useWorkflowSelector 透传所有属性和方法 ---
        ...selector,

        // --- 新增的、经过处理的数据 ---
        filteredAndSortedWorkflows,
    };
}