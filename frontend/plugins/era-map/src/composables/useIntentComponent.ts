import type {Ref} from 'vue';
import {computed, watch} from 'vue';
import type {Instruction} from '#/game-logic/types';
import {InstructionStatus, InstructionType} from '#/game-logic/types';
import {useInstructionStreamStore} from '#/stores/useInstructionStreamStore';
import {useWorldStateStore} from '#/stores/useWorldStateStore';
import {
    useFilteredWorkflowSelector,
    useStructuredWorkflowStream,
    type WorkflowFilter
} from "@yaesandbox-frontend/core-services/composables";
import type {WorkflowConfig} from "@yaesandbox-frontend/core-services/types";

/**
 * 根据指令类型，定义对工作流的要求。
 * 这是连接业务意图和工作流能力的桥梁。
 */
const WORKFLOW_REQUIREMENTS: Record<InstructionType, WorkflowFilter> = {
    [InstructionType.ENRICH_OBJECT]: {
        expectedInputs: ['user_prompt', 'object_type', 'existing_properties'],
        requiredTags: ['enrichment'], // 假设我们用tag来做硬性筛选
    }
};


/**
 * 将扁平化的对象转换回其原始的嵌套结构。
 * @param flatObject - 例如 { 'a__b': 1, 'a__c': 2 }
 * @returns 嵌套对象 - 例如 { a: { b: 1, c: 2 } }
 */
function unflattenObject(flatObject: Record<string, any>): Record<string, any>
{
    const result: any = {};
    for (const key in flatObject)
    {
        const keys = key.split('__');
        keys.reduce((acc, currentKey, index) =>
        {
            if (index === keys.length - 1)
            {
                // 在最后一层赋值
                acc[currentKey] = flatObject[key];
            }
            else
            {
                // 如果路径不存在，则创建对象
                acc[currentKey] = acc[currentKey] || {};
            }
            return acc[currentKey];
        }, result);
    }
    return result;
}

/**
 * 驱动单个 "意图组件" 的核心逻辑。
 * 它封装了工作流的选择、执行、状态管理和与全局Store的通信。
 * @param instructionRef - 对单个指令对象的响应式引用
 */
export function useIntentComponent(instructionRef: Ref<Instruction>)
{
    const instructionStore = useInstructionStreamStore();
    const worldStateStore = useWorldStateStore();

    // --- 1. 工作流选择 ---
    const workflowFilter = computed<WorkflowFilter>(() => WORKFLOW_REQUIREMENTS[instructionRef.value.type]);

    // 为每个意图组件实例创建一个唯一的 storageKey，用于持久化其工作流选择
    const workflowStorageKey = `intent-component-workflow--${instructionRef.value.id}`;

    const workflowSelector = useFilteredWorkflowSelector(workflowStorageKey, workflowFilter);
    const {selectedWorkflowConfig} = workflowSelector;

    // --- 2. 工作流执行 ---
    const stream = useStructuredWorkflowStream();

    // --- 3. 状态与计算属性 ---
    const isGenerating = computed(() => instructionRef.value.status === InstructionStatus.GENERATING);
    const hasValidProposal = computed(() =>
        instructionRef.value.status === InstructionStatus.PROPOSED &&
        instructionRef.value.aiProposal &&
        Object.keys(instructionRef.value.aiProposal).length > 0
    );

    // --- 4. 核心动作 ---

    /**
     * 触发AI生成。
     * 如果没有选择工作流，会由UI（WorkflowSelectorButton）引导用户选择。
     * @param config - 由 WorkflowSelectorButton 点击时传入的已选定工作流配置。
     */
    async function generate(config: WorkflowConfig)
    {
        instructionStore.updateInstruction(instructionRef.value.id, {
            status: InstructionStatus.GENERATING,
            aiProposal: null,
            error: null
        });

        const targetObject = worldStateStore.logicalGameMap?.findObjectById(instructionRef.value.context.targetObjectId!);

        const workflowInputs = {
            user_prompt: instructionRef.value.userInput.prompt,
            object_type: targetObject?.type ?? '',
            existing_properties: JSON.stringify(targetObject?.properties ?? {}, null, 2),
        };

        // 检查所选工作流是否满足所有输入要求
        const missingInputs = (workflowFilter.value.expectedInputs || []).filter(
            input => !(config.workflowInputs || []).includes(input)
        );

        if (missingInputs.length > 0)
        {
            const errorMsg = `选择的工作流缺少必需的输入: ${missingInputs.join(', ')}`;
            console.error(errorMsg);
            instructionStore.updateInstruction(instructionRef.value.id, {status: InstructionStatus.ERROR, error: errorMsg});
            return;
        }

        await stream.execute(config, workflowInputs);
    }

    /**
     * 将AI的提案应用到世界状态中。
     */
    function applyProposal()
    {
        const proposal = instructionRef.value.aiProposal;
        if (!hasValidProposal.value || !proposal) return;

        const targetObjectId = instructionRef.value.context.targetObjectId;
        if (!targetObjectId) return;

        instructionStore.updateInstruction(instructionRef.value.id, {status: InstructionStatus.APPLYING});

        try
        {
            // 注意：AI返回的flatData是纯字符串，这里可能需要类型转换。
            // 暂时我们假设所有属性都是字符串，或者在unflatten之后再处理。
            const nestedProposalData = unflattenObject(proposal);

            worldStateStore.applyProposal({
                targetObjectId,
                data: nestedProposalData,
            });

            instructionStore.updateInstruction(instructionRef.value.id, {status: InstructionStatus.APPLIED});
        } catch (e)
        {
            const errorMsg = `应用提案时出错: ${(e as Error).message}`;
            console.error(errorMsg);
            instructionStore.updateInstruction(instructionRef.value.id, {status: InstructionStatus.ERROR, error: errorMsg});
        }
    }

    /**
     * 放弃当前指令和提案。
     */
    function discard()
    {
        instructionStore.updateInstruction(instructionRef.value.id, {status: InstructionStatus.DISCARDED});
    }

    // --- 5. 监听与副作用 ---

    // 监听流的状态变化，并更新指令
    watch(stream.isFinished, (finished) =>
    {
        if (finished && !stream.error.value)
        {
            instructionStore.updateInstruction(instructionRef.value.id, {
                status: InstructionStatus.PROPOSED,
                aiProposal: stream.flatTextData.value, // 提案是扁平化的、纯字符串的 flatData
            });
        }
    });

    watch(stream.error, (err) =>
    {
        if (err)
        {
            instructionStore.updateInstruction(instructionRef.value.id, {
                status: InstructionStatus.ERROR,
                error: err.message,
            });
        }
    });

    // --- 6. 返回接口 ---
    return {
        // 状态
        isGenerating,
        hasValidProposal,
        thinkingProcess: stream.thinkingProcess, // 透传思考过程
        rawProposalData: stream.rawStreamData, // 透传原始数据以供调试或复杂渲染

        // 动作
        generate,
        applyProposal,
        discard,

        // 子Composable实例，供UI直接使用
        workflowSelector,
    };
}