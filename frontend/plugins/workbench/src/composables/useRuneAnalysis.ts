import {computed, onMounted, ref, type Ref, watch} from "vue";
import type {AbstractRuneConfig, RuneAnalysisResult, TuumConfig} from "#/types/generated/workflow-config-api-client";
import {useRuneAnalysisStore} from "#/stores/useRuneAnalysisStore.ts";
import {useDebounceFn} from "@vueuse/core";
import {useWorkbenchStore} from "#/stores/workbenchStore.ts";

/**
 * 一个 Vue Composable，用于对符文（Rune）配置进行实时分析。
 * @param rune 包含符文配置的响应式 Ref。
 * @param configId 符文的唯一ID Ref。
 * @param tuumContext (可选) 包含符文所在枢机上下文的响应式 Ref。
 */
export function useRuneAnalysis(rune: Ref<AbstractRuneConfig | null>, configId: Ref<string>, tuumContext: Ref<TuumConfig | null>)
{
    const runeAnalysisStore = useRuneAnalysisStore();
    const workbenchStore = useWorkbenchStore();
    const analysisResult = ref<RuneAnalysisResult | null>(null);

    const hasConsumedVariables = computed(() =>
        (analysisResult.value?.consumedVariables?.length || 0) > 0
    );

    const hasProducedVariables = computed(() =>
        (analysisResult.value?.producedVariables?.length || 0) > 0
    );

    const debouncedExecuteAnalysis = useDebounceFn(executeAnalysis, 300);

    async function executeAnalysis(newRune: AbstractRuneConfig | null, context: TuumConfig | null): Promise<void>
    {
        try
        {
            // 如果没有传入符文或其类型，则清空分析结果
            if (!newRune || !newRune.runeType)
            {
                analysisResult.value = null;
                return;
            }

            // 3. 使用这个副本进行分析
            if (newRune)
            {
                analysisResult.value = await runeAnalysisStore.analyzeRune(
                    newRune,
                    newRune.configId,
                    context
                ) || null;
                if (!analysisResult.value)
                {
                    console.error(`[useRuneAnalysis] 分析结果为null`);
                }
            }
            else
            {
                analysisResult.value = null;
            }
            // console.log(`[useRuneAnalysis] 分析结果: `, analysisResult.value);
        } catch (error: any)
        {
            console.error(`[useRuneAnalysis] 在分析符文 ${configId.value} 时失败: `, error);
            analysisResult.value = null; // 失败时也清空结果
        }
    }

    // 同时监听符文和其上下文的变化
    watch([rune, tuumContext], ([newRune, newContext]) =>
    {
        debouncedExecuteAnalysis(newRune, newContext)
    }, {immediate: false, deep: true});

    onMounted(() => executeAnalysis(rune.value, tuumContext.value));

    return {
        analysisResult,
        hasConsumedVariables,
        hasProducedVariables,
        refresh: () => executeAnalysis(rune.value, tuumContext.value)
    };
}