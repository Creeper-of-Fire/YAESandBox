import {computed, onMounted, ref, type Ref, watch} from "vue";
import type {AbstractRuneConfig, RuneAnalysisResult} from "@/app-workbench/types/generated/workflow-config-api-client";
import {useRuneAnalysisStore} from "@/app-workbench/stores/useRuneAnalysisStore.ts";
import {useDebounceFn} from "@vueuse/core";

export function useRuneAnalysis(rune: Ref<AbstractRuneConfig | null>, configId: Ref<string>)
{
    const runeAnalysisStore = useRuneAnalysisStore();
    const analysisResult = ref<RuneAnalysisResult | null>(null);

    const hasConsumedVariables = computed(() =>
        (analysisResult.value?.consumedVariables?.length || 0) > 0
    );

    const hasProducedVariables = computed(() =>
        (analysisResult.value?.producedVariables?.length || 0) > 0
    );

    const debouncedExecuteAnalysis = useDebounceFn(executeAnalysis, 300);

    async function executeAnalysis(newRune: AbstractRuneConfig | null)
    {
        try
        {
            // 如果没有传入符文，则清空分析结果
            if (!newRune)
            {
                analysisResult.value = null;
                return;
            }

            if (newRune)
            {
                analysisResult.value = await runeAnalysisStore.analyzeRune(
                    newRune,
                    configId.value
                ) || null;
                if (!analysisResult.value)
                {
                    console.error(`[useRuneAnalysis] 分析结果为null`);
                }
            }
            // console.log(`[useRuneAnalysis] 分析结果: `, analysisResult.value);
        } catch (error: any)
        {
            console.error(`[useRuneAnalysis] 在分析符文 ${configId.value} 时失败: `, error);
            analysisResult.value = null; // 失败时也清空结果
        }
    }

    watch(rune, async (newRune) =>
    {
        await debouncedExecuteAnalysis(newRune)
    }, {immediate: false, deep: true});

    onMounted(async () => await executeAnalysis(rune.value));

    return {
        analysisResult,
        hasConsumedVariables,
        hasProducedVariables,
        refresh: () => executeAnalysis(rune.value)
    };
}