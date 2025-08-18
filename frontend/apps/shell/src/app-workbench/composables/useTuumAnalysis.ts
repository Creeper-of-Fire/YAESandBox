// frontend/src/app-workbench/composables/useTuumAnalysis.ts
import {onMounted, ref, type Ref, watch} from "vue";
import type {TuumAnalysisResult, TuumConfig} from "@/app-workbench/types/generated/workflow-config-api-client";
import {useTuumAnalysisStore} from "@/app-workbench/stores/useTuumAnalysisStore";
import {useDebounceFn} from "@vueuse/core";

/**
 * 一个 Vue Composable，用于对枢机（Tuum）配置进行实时分析。
 * @param tuum - 一个包含 TuumConfig 的响应式引用 (Ref)。
 * @returns 返回包含分析结果、加载状态和刷新方法的对象。
 */
export function useTuumAnalysis(tuum: Ref<TuumConfig | null>)
{
    const tuumAnalysisStore = useTuumAnalysisStore();
    const analysisResult = ref<TuumAnalysisResult | null>(null);
    const isLoading = ref(false);

    // 使用 vueuse 的 useDebounceFn 创建一个防抖函数，避免过于频繁的API调用
    const debouncedExecuteAnalysis = useDebounceFn(executeAnalysis, 300);

    /**
     * 执行分析的核心函数。
     * @param newTuum - 最新的枢机配置数据。
     */
    async function executeAnalysis(newTuum: TuumConfig | null)
    {
        if (!newTuum)
        {
            analysisResult.value = null;
            return;
        }

        isLoading.value = true;
        try
        {
            // 调用 store 中的分析方法
            analysisResult.value = await tuumAnalysisStore.analyzeTuum(newTuum) || null;
        } catch (error)
        {
            console.error(`[useTuumAnalysis] 在分析枢机 ${newTuum.name} 时失败: `, error);
            analysisResult.value = null; // 失败时清空结果
        } finally
        {
            isLoading.value = false;
        }
    }

    // 深度监听 tuum Ref 的变化，并在变化时触发防抖的分析函数
    watch(tuum, async (newTuum) =>
    {
        await debouncedExecuteAnalysis(newTuum);
    }, {
        deep: true,       // 深度监听，因为 TuumConfig 是一个复杂的对象
        immediate: false
    });

    onMounted(async () => await executeAnalysis(tuum.value));

    return {
        /**
         * 枢机分析结果的响应式引用。
         */
        analysisResult,
        /**
         * 指示当前是否正在进行分析的布尔值。
         */
        isLoading,
        /**
         * 手动触发一次分析的函数。
         */
        refresh: () => executeAnalysis(tuum.value),
    };
}