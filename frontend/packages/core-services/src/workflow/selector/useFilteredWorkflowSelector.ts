import {computed, type Ref} from 'vue';
import {useWorkflowSelector} from './useWorkflowSelector.ts';
import type {WorkflowConfig} from "../../types";


/**
 * 定义输入匹配模式
 * - strict: 必须完美匹配所有输入，不多不少。
 * - normal: 可以接受场景提供的额外输入（被忽略）。
 * - relaxed: 允许工作流缺少部分输入。
 */
export type InputMatchingMode  = 'strict' | 'normal' | 'relaxed';
/**
 * 定义标签匹配模式
 * - need: 标签是必须的
 * - prefer: 标签是推荐的
 */
export type TagMatchingMode = 'need' | 'prefer';

export interface MatchingOptions {
    inputs: InputMatchingMode;
    tags: TagMatchingMode;
}

/**
 * 定义了用于筛选工作流的条件。
 */
export interface WorkflowFilter
{
    expectedInputs?: string[];
    requiredTags?: string[];
}

/**
 * 增强后的工作流对象，包含了丰富的匹配信息用于UI渲染。
 */
export interface EnhancedWorkflow {
    id: string;
    resource: WorkflowConfig;
    // 排序评分
    score: {
        unprovidedInputs: number; // 工作流需要但场景未提供的输入（致命缺陷）
        unconsumedInputs: number; // 场景提供但工作流未使用的输入（资源浪费）
        missingTags: number;      // 工作流缺少的必需标签
        extraTags: number;        // 工作流多出的额外标签（轻微加分或中性）
    };
    // 用于UI渲染的详细匹配细节
    matchDetails: {
        // 输入参数的分类
        inputs: {
            // key: 输入参数名, value: 状态
            [key: string]: 'matched' | 'unprovided' | 'unconsumed';
        };
        // 标签的分类
        tags: {
            // key: 标签名, value: 状态
            [key: string]: 'matched' | 'missing' | 'extra';
        };
    };
}

/**
 * 一个包装器 Composable，在 useWorkflowSelector 的基础上增加了强大的筛选和排序逻辑。
 *
 * @param storageKey 用于持久化用户选择的唯一键。
 * @param filter 一个响应式的筛选条件对象 Ref。
 * @param options 一个响应式的、包含独立匹配模式的配置对象 Ref。
 */
export function useFilteredWorkflowSelector(storageKey: string, filter: Ref<WorkflowFilter>, options: Ref<MatchingOptions>) {

    const selector = useWorkflowSelector(storageKey);

    const filteredAndSortedWorkflows = computed<EnhancedWorkflow[]>(() => {
        if (!selector.availableWorkflows.value) return [];

        const expectedInputs = new Set(filter.value.expectedInputs || []);
        const requiredTags = new Set(filter.value.requiredTags || []);
        const currentOptions = options.value;

        const enhanced = selector.availableWorkflows.value
            // --- 步骤 A: 增强与评分 (我们先计算所有信息，再根据模式过滤) ---
            .map(workflow => {
                const wfInputs = new Set(workflow.resource.workflowInputs || []);
                const wfTags = new Set(workflow.resource.tags || []);

                // 1. 计算输入匹配度
                const matchedInputs = new Set([...expectedInputs].filter(x => wfInputs.has(x)));
                const unconsumedInputs = new Set([...expectedInputs].filter(x => !wfInputs.has(x))); // 场景提供，但WF用不上
                const unprovidedInputs = new Set([...wfInputs].filter(x => !expectedInputs.has(x))); // WF需要，但场景没提供

                // 2. 计算标签匹配度
                const matchedTags = new Set([...requiredTags].filter(x => wfTags.has(x)));
                const missingTags = new Set([...requiredTags].filter(x => !wfTags.has(x)));
                const extraTags = new Set([...wfTags].filter(x => !requiredTags.has(x)));

                // 3. 构建详细的 matchDetails 对象
                const details: EnhancedWorkflow['matchDetails'] = {
                    inputs: {},
                    tags: {}
                };
                for (const input of matchedInputs) details.inputs[input] = 'matched';
                // for (const input of unconsumedInputs) details.inputs[input] = 'unconsumed';
                for (const input of unprovidedInputs) details.inputs[input] = 'unprovided';

                // 将所有工作流自身的标签都展示出来
                for (const tag of wfTags) {
                    details.tags[tag] = requiredTags.has(tag) ? 'matched' : 'extra';
                }


                return {
                    ...workflow,
                    score: {
                        unprovidedInputs: unprovidedInputs.size,
                        unconsumedInputs: unconsumedInputs.size,
                        missingTags: missingTags.size,
                        extraTags: extraTags.size,
                    },
                    matchDetails: details
                } as EnhancedWorkflow;
            })
            // --- 步骤 B: 根据正交配置进行过滤 ---
            .filter(workflow => {
                // 过滤条件1: 工作流需要的输入场景无法提供。
                if (currentOptions.inputs !== 'relaxed' && workflow.score.unprovidedInputs > 0) {
                    return false;
                }

                // 过滤条件2: 根据输入的 'strict' 模式
                if (currentOptions.inputs === 'strict' && workflow.score.unconsumedInputs > 0) {
                    return false; // 严格模式下，不允许场景提供工作流用不上的输入
                }

                // 过滤条件3: 根据标签的 'need' 模式
                if (currentOptions.tags === 'need' && workflow.score.missingTags > 0) {
                    return false; // "必须"模式下，工作流不能缺少任何一个必需标签
                }

                // 所有过滤条件都通过
                return true;
            })
            // --- 步骤 C: 根据正交配置进行排序 ---
            .sort((a, b) => {
                // 排序规则1: "缺少必需标签"越少越靠前。在 'prefer' 模式下这是最重要的排序依据。
                if (a.score.missingTags !== b.score.missingTags) {
                    return a.score.missingTags - b.score.missingTags;
                }

                // 排序规则2: "资源浪费(unconsumedInputs)"越少越靠前。这是 'normal' 和 'relaxed' 模式的核心区别。
                if (a.score.unconsumedInputs !== b.score.unconsumedInputs) {
                    // 在 relaxed 模式下，我们不关心 unconsumedInputs，视其为0
                    const aPenalty = currentOptions.inputs === 'relaxed' ? 0 : a.score.unconsumedInputs;
                    const bPenalty = currentOptions.inputs === 'relaxed' ? 0 : b.score.unconsumedInputs;
                    if (aPenalty !== bPenalty) {
                        return aPenalty - bPenalty;
                    }
                }

                // 排序规则3: 按名称字母排序作为备用
                return a.resource.name.localeCompare(b.resource.name);
            });

        return enhanced;
    });

    return {
        ...selector,
        filteredAndSortedWorkflows,
    };
}