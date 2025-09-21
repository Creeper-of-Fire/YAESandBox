import {defineStore} from 'pinia';
import {computed, ref} from 'vue';
import type {
    AbstractRuneConfig,
    RuneAnalysisRequest,
    RuneAnalysisResult,
    TuumConfig
} from '#/types/generated/workflow-config-api-client';
import {WorkflowAnalysisService} from '#/types/generated/workflow-config-api-client';

interface RuneAnalysisCache
{
    [key: string]: RuneAnalysisResult;
}

interface PendingRequestCache
{
    [configString: string]: Promise<RuneAnalysisResult | undefined>;
}

/**
 * 一个简单的、非加密的字符串哈希函数。
 * 性能极高，非常适合用于生成缓存键。
 * @param str 输入字符串
 * @returns 一个正整数哈希值
 */
function simpleHash(str: string): number
{
    let hash = 5381;
    let i = str.length;
    while (i)
    {
        hash = (hash * 33) ^ str.charCodeAt(--i);
    }
    return hash >>> 0; // 确保为正整数
}

export const useRuneAnalysisStore = defineStore('runeAnalysis', () =>
{

    const analysisCache = ref<RuneAnalysisCache>({});

    const pendingRequests = ref<PendingRequestCache>({});

    async function _analyze(runeConfig: AbstractRuneConfig)
    {
        // 使用包含/不包含上下文的完整配置作为缓存键，以区分不同上下文下的相同符文
        const configKey = simpleHash(JSON.stringify(runeConfig));

        if (analysisCache.value[configKey])
        {
            // 如果缓存中有，则直接返回
            return analysisCache.value[configKey];
        }

        if (configKey in pendingRequests.value)
        {
            return pendingRequests.value[configKey];
        }

        // 3. 发起新请求（使用 Async IIFE 模式）
        // (async () => { ... })() 会立即执行这个异步函数，
        // 并立即返回一个 Promise 对象。
        // - 如果函数内部成功 `return result;`，这个 Promise 就会 resolve 并带有 result 值。
        // - 如果函数内部 `throw error;`，这个 Promise 就会 reject 并带有 error。
        // 这完美地替代了 new Promise(executor) 的用法，且更安全、简洁。
        const analysisPromise = (async () =>
        {
            try
            {
                // 构造请求体，包含符文
                const requestBody: RuneAnalysisRequest = {
                    runeToAnalyze: runeConfig
                };
                const result = await WorkflowAnalysisService.postApiV1WorkflowsConfigsAnalysisAnalyzeRune({
                    requestBody: requestBody,
                });
                // 请求成功，存入缓存
                analysisCache.value[configKey] = result;
                return result; // <--- 正确地返回结果，这将 resolve Promise
            } catch (error)
            {
                console.error(`在分析符文 ${runeConfig.configId} 时失败: `, error);
                throw error; // <--- 正确地抛出错误，这将 reject Promise
            } finally
            {
                // 无论成功或失败，都从在途请求中移除
                delete pendingRequests.value[configKey];
            }
        })(); // <--- 最后的 () 表示立即执行

        // 将这个新创建的 Promise 存入在途请求映射中
        pendingRequests.value[configKey] = analysisPromise;

        // 返回这个 Promise，让调用方去 await
        return analysisPromise;
    }

    // 分析单个符文的动作，带有缓存功能
    const analyzeRune = _analyze;

    // 清除缓存的动作（例如当工作流发生重大变化时）
    const clearCache = () =>
    {
        analysisCache.value = {};
    };

    return {
        analyzeRune,
        clearCache,
    };
});