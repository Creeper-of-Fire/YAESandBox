import {computed, onMounted, ref, type Ref, watch} from "vue";
import type {AbstractModuleConfig, ModuleAnalysisResult} from "@/app-workbench/types/generated/workflow-config-api-client";
import {useModuleAnalysisStore} from "@/app-workbench/stores/useModuleAnalysisStore.ts";
import {useDebounceFn} from "@vueuse/core";

export function useModuleAnalysis(module: Ref<AbstractModuleConfig | null>, configId: Ref<string>)
{
    const moduleAnalysisStore = useModuleAnalysisStore();
    const analysisResult = ref<ModuleAnalysisResult | null>(null);

    const hasConsumedVariables = computed(() =>
        (analysisResult.value?.consumedVariables?.length || 0) > 0
    );

    const hasProducedVariables = computed(() =>
        (analysisResult.value?.producedVariables?.length || 0) > 0
    );

    const debouncedExecuteAnalysis = useDebounceFn(executeAnalysis, 300);

    async function executeAnalysis(newModule: AbstractModuleConfig | null)
    {
        try
        {
            // 如果没有传入模块，则清空分析结果
            if (!newModule)
            {
                analysisResult.value = null;
                return;
            }

            if (newModule)
            {
                analysisResult.value = await moduleAnalysisStore.analyzeModule(
                    newModule,
                    configId.value
                ) || null;
                if (!analysisResult.value)
                {
                    console.error(`[useModuleAnalysis] 分析结果为null`);
                }
            }
            // console.log(`[useModuleAnalysis] 分析结果: `, analysisResult.value);
        } catch (error: any)
        {
            console.error(`[useModuleAnalysis] 在分析模块 ${configId.value} 时失败: `, error);
            analysisResult.value = null; // 失败时也清空结果
        }
    }

    watch(module, async (newModule) =>
    {
        await debouncedExecuteAnalysis(newModule)
    }, {immediate: false, deep: true});

    onMounted(async () => await executeAnalysis(module.value));

    return {
        analysisResult,
        hasConsumedVariables,
        hasProducedVariables,
        refresh: () => executeAnalysis(module.value)
    };
}