import {defineStore} from 'pinia';
import {computed, ref} from 'vue';
import type {
    AbstractRuneConfig,
    RuneAnalysisRequest,
    RuneAnalysisResult,
    TuumConfig
} from '@/app-workbench/types/generated/workflow-config-api-client';
import {WorkflowAnalysisService} from '@/app-workbench/types/generated/workflow-config-api-client';

interface RuneAnalysisCache
{
    [key: string]: RuneAnalysisResult;
}

interface PendingRequestCache
{
    [configString: string]: Promise<RuneAnalysisResult | undefined>;
}

export const useRuneAnalysisStore = defineStore('runeAnalysis', () =>
{

    const analysisCache = ref<RuneAnalysisCache>({});

    const pendingRequests = ref<PendingRequestCache>({});

    async function _analyze(runeConfig: AbstractRuneConfig, runeId: string, tuumContext: TuumConfig | null = null)
    {
        // 使用包含/不包含上下文的完整配置作为缓存键，以区分不同上下文下的相同符文
        const configString = JSON.stringify({config: runeConfig, context: tuumContext}); // 使用字符串化的配置作为缓存键

        if (analysisCache.value[configString])
        {
            // 如果缓存中有，则直接返回
            return analysisCache.value[configString];
        }

        if (configString in pendingRequests.value)
        {
            return pendingRequests.value[configString];
        }

        // 3. 发起新请求（使用 Async IIFE 模式）
        // (async () => { ... })() 会立即执行这个异步函数，
        // 并立即返回一个 Promise 对象。
        // - 如果函数内部成功 `return result;`，这个 Promise 就会 resolve 并带有 result 值。
        // - 如果函数内部 `throw error;`，这个 Promise 就会 reject 并带有 error。
        // 这完美地替代了 new Promise(executor) 的用法，且更安全、简洁。
        const analysisPromise = (async () => {
            try {
                // 构造请求体，包含符文和可选的枢机上下文
                const requestBody: RuneAnalysisRequest = {
                    runeToAnalyze: runeConfig,
                    // 如果 tuumContext 为 null，则后端会收到 undefined，这符合预期
                    tuumContext: tuumContext ?? undefined
                };
                const result = await WorkflowAnalysisService.postApiV1WorkflowsConfigsAnalysisAnalyzeRune({
                    requestBody: requestBody,
                });
                // 请求成功，存入缓存
                analysisCache.value[configString] = result;
                return result; // <--- 正确地返回结果，这将 resolve Promise
            } catch (error) {
                console.error(`在分析符文 ${runeId} 时失败: `, error);
                throw error; // <--- 正确地抛出错误，这将 reject Promise
            } finally {
                // 无论成功或失败，都从在途请求中移除
                delete pendingRequests.value[configString];
            }
        })(); // <--- 最后的 () 表示立即执行

        // 将这个新创建的 Promise 存入在途请求映射中
        pendingRequests.value[configString] = analysisPromise;

        // 返回这个 Promise，让调用方去 await
        return analysisPromise;
    }

    // 分析单个符文的动作，带有缓存功能
    const analyzeRune = _analyze;

    // 从缓存中获取分析结果的getter
    const getAnalysisResult = computed(() => (runeConfig: AbstractRuneConfig) =>
    {
        const configString = JSON.stringify(runeConfig);
        return analysisCache.value[configString];
    });

    // 清除缓存的动作（例如当工作流发生重大变化时）
    const clearCache = () =>
    {
        analysisCache.value = {};
    };

    return {
        analysisCache,
        analyzeRune,
        getAnalysisResult,
        clearCache,
    };
});