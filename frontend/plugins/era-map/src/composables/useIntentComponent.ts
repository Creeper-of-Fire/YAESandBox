import {type Ref, toRefs} from 'vue';
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
 * 1. 接收工作流配置并执行。
 * 2. 管理指令在执行->应用过程中的状态。
 * @param instructionRef - 对单个指令对象的响应式引用
 * @param workflowFilter
 */
export function useIntentComponent(instructionRef: Ref<Instruction>,workflowFilter: Ref<WorkflowFilter>)
{
    const instructionStore = useInstructionStreamStore();
    const worldStateStore = useWorldStateStore();

    // --- 1. 工作流执行 ---
    const stream = useStructuredWorkflowStream();

    // 2. 状态与计算属性
    const { status, context, userInput } = toRefs(instructionRef.value);

    const isGenerating = computed(() => status.value === InstructionStatus.GENERATING);
    const hasValidProposal = computed(() =>
        status.value === InstructionStatus.PROPOSED &&
        instructionRef.value.aiProposal &&
        Object.keys(instructionRef.value.aiProposal).length > 0
    );

    // --- 3. 核心动作 ---

    /**
     * 触发AI生成。
     * @param config - 由UI层（例如 WorkflowSelectorButton）传入的已选定工作流配置。
     */
    async function generate(config: WorkflowConfig) {
        instructionStore.updateInstruction(instructionRef.value.id, {
            status: InstructionStatus.GENERATING,
            aiProposal: null,
            error: null
        });

        const targetObject = worldStateStore.logicalGameMap?.findObjectById(context.value.targetObjectId!);

        const workflowInputs = {
            user_prompt: userInput.value.prompt,
            object_type: targetObject?.type ?? '',
            existing_properties: JSON.stringify(targetObject?.properties ?? {}, null, 2),
        };

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

        // 动作
        generate,
        applyProposal,
        discard,
    };
}