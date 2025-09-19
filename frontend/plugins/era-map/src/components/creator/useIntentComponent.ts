import {computed, type Ref, toRefs, watch} from 'vue';
import {useInstructionStreamStore} from '#/stores/useInstructionStreamStore';
import {useWorldStateStore} from '#/stores/useWorldStateStore';
import {useStructuredWorkflowStream} from "@yaesandbox-frontend/core-services/composables";
import type {WorkflowConfig} from "@yaesandbox-frontend/core-services/types";
import {type Instruction, InstructionStatus, InstructionType} from "#/components/creator/instruction.ts";


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
 */
export function useIntentComponent(instructionRef: Ref<Instruction>)
{
    const instructionStore = useInstructionStreamStore();
    const worldStateStore = useWorldStateStore();

    // --- 1. 工作流执行 ---
    const stream = useStructuredWorkflowStream();

    // 2. 状态与计算属性
    const {status, context, userInput} = toRefs(instructionRef.value);

    const isGenerating = computed(() => status.value === InstructionStatus.GENERATING);
    const hasValidProposal = computed(() =>
        status.value === InstructionStatus.PROPOSED &&
        instructionRef.value.aiProposal &&
        Object.keys(instructionRef.value.aiProposal).length > 0
    );

    // --- 3. 核心动作 ---

    /**
     * 触发AI生成。
     * @param config - 由UI层（例如 WorkflowSelectorButton.vue）传入的已选定工作流配置。
     */
    async function generate(config: WorkflowConfig)
    {
        instructionStore.updateInstruction(instructionRef.value.id, {
            status: InstructionStatus.GENERATING,
            aiProposal: null,
            error: null
        });

        const targetObject = worldStateStore.logicalGameMap?.findObjectById(context.value.targetObjectId!);

        let workflowInputs = {};
        if (instructionRef.value.type === InstructionType.INITIALIZE_COMPONENT)
        {
            // 为新指令类型构建输入
            workflowInputs = {
                user_prompt: userInput.value.prompt,
                object_type: targetObject?.type ?? '',
                component_type: context.value.componentType ?? '',
            };
        }
        else
        {
            // 保持旧的输入结构
            workflowInputs = {
                user_prompt: userInput.value.prompt,
                object_type: targetObject?.type ?? '',
                existing_properties: JSON.stringify(targetObject?.properties ?? {}, null, 2),
            };
        }

        await stream.execute(config, workflowInputs);
    }

    /**
     * 将AI的提案应用到世界状态中。
     */
    function applyProposal(proposalData: Record<string, any>)
    {
        const instruction = instructionRef.value;
        const targetObjectId = instruction.context.targetObjectId;
        if (!targetObjectId) return;

        // 1. 从世界状态中获取当前对象的属性
        const targetObject = worldStateStore.logicalGameMap?.findObjectById(targetObjectId);
        if (!targetObject)
        {
            console.error(`Cannot apply proposal: Target object ${targetObjectId} not found in world state.`);
            // 可以在此更新指令状态为 ERROR
            return;
        }
        // 创建一个当前属性的安全副本，用于计算
        const currentProperties = JSON.parse(JSON.stringify(targetObject.properties || {}));

        // 2. 将AI返回的扁平化数据转换为嵌套对象
        const nestedProposalData = unflattenObject(proposalData);

        // 3. 根据指令类型，计算出最终的、全新的 `newProperties` 对象
        let newProperties: Record<string, any>;

        if (instruction.type === InstructionType.INITIALIZE_COMPONENT)
        {
            const componentType = instruction.context.componentType;
            if (!componentType)
            {
                console.error("Cannot initialize component: componentType is missing in instruction context.");
                // 更新指令状态为 ERROR
                return;
            }
            newProperties = {
                ...currentProperties,
                [componentType]: nestedProposalData // 将提案数据作为新组件直接赋值/覆盖
            };
        }
        else
        { // 默认为 ENRICH_OBJECT
            newProperties = {
                ...currentProperties,
                ...nestedProposalData // 执行浅合并
            };
        }

        instructionStore.updateInstruction(instruction.id, {status: InstructionStatus.APPLYING});

        try
        {
            // 4. 调用 store 中纯粹的更新函数，传递最终计算结果
            worldStateStore.applyProposal({
                targetObjectId,
                newProperties, // <-- 只传递最终结果
            });

            instructionStore.updateInstruction(instruction.id, {status: InstructionStatus.APPLIED});
        } catch (e)
        {
            const errorMsg = `应用提案时出错: ${(e as Error).message}`;
            console.error(errorMsg);
            instructionStore.updateInstruction(instruction.id, {status: InstructionStatus.ERROR, error: errorMsg});
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
            let aiProposal;
            console.log('stream.flatTextData.value:', stream.flatTextData.value);
            if ('context' in stream.flatTextData.value)
            {
                aiProposal = JSON.parse(stream.flatTextData.value['context']);
            }
            else
            {
                aiProposal = stream.flatTextData.value;
            }
            instructionStore.updateInstruction(instructionRef.value.id, {
                status: InstructionStatus.PROPOSED,
                aiProposal: aiProposal, // 提案是扁平化的、纯字符串的 flatData
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