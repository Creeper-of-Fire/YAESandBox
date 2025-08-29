import {computed, type Ref, watch} from 'vue';
import {useWorkflowStream} from '@yaesandbox-frontend/core-services/composables';
import {getText, getThink, parseNumber} from '#/utils/workflowParser';
import type {ComplexPropertyValue} from '#/types/streaming';
import type {SchemaField} from '#/types/generator';

/**
 * 一个更高阶的 Composable，用于处理流式工作流并将其映射到指定的结构化对象。
 *
 * @template T - 期望输出的结构化对象的类型，例如 Partial<Item>。
 * @param targetRef - 一个 Ref，用于接收流式生成的数据。Composable 会直接修改这个 Ref。
 * @param schema - 一个 Schema 数组，描述了如何将流数据映射到 targetRef 的字段上。
 */
export function useStructuredWorkflowStream<T extends object>(
    targetRef: Ref<T | null>,
    schema: SchemaField[]
)
{
    // 底层的、非结构化的流式数据
    const {
        data: rawStreamData,
        isLoading,
        error,
        isFinished,
        execute,
    } = useWorkflowStream<Record<string, ComplexPropertyValue>>();

    // 监听底层流数据的变化，并更新我们结构化的 targetRef
    watch(rawStreamData, (newData) =>
    {
        if (!newData)
        {
            targetRef.value = null;
            return;
        }

        // 如果 targetRef 还是 null，基于 schema 创建一个初始空对象
        if (!targetRef.value)
        {
            targetRef.value = {} as T;
        }

        const newTarget = {...targetRef.value} as Record<string, any>;

        for (const field of schema)
        {
            const streamValue = newData[field.key];
            if (streamValue !== undefined)
            {
                if (field.type === 'number')
                {
                    newTarget[field.key] = parseNumber(streamValue);
                } else
                {
                    newTarget[field.key] = getText(streamValue);
                }
            }
        }
        targetRef.value = newTarget as T;
    }, {deep: true});

    // 聚合所有字段的思考过程
    const thinkingProcess = computed(() =>
    {
        if (!rawStreamData.value) return '';

        return schema
            .map(field => getThink(rawStreamData.value![field.key]))
            .filter(Boolean) // 过滤掉 null 或空字符串
            .join('\n\n---\n\n'); // 用分隔符让思考过程更清晰
    });

    // 清理函数
    function clear()
    {
        rawStreamData.value = null;
        targetRef.value = null;
    }

    return {
        isLoading,
        error,
        isFinished,
        execute,
        thinkingProcess,
        clear,
    };
}