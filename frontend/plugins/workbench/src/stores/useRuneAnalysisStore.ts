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

// 这是一个全局的、用于实例到内容键映射的缓存。
// 使用 WeakMap 的好处是当对象被垃圾回收时，这里的条目也会被自动移除，不会造成内存泄漏。
const objectToKeyCache = new WeakMap<object, string>();

/**
 * 高性能地获取一个对象的稳定内容键。
 * 优先从 WeakMap 缓存中读取，只有在新实例出现时才执行序列化和哈希。
 * @param obj 要获取键的对象 (可以是 runeConfig 或 tuumContext)
 * @returns 对象的稳定内容键
 */
function getContentKey(obj: object | null): string
{
    if (obj === null)
    {
        return 'null'; // 处理 null 的情况
    }

    // 1. 快速路径：检查此对象实例是否已在缓存中
    if (objectToKeyCache.has(obj))
    {
        return objectToKeyCache.get(obj)!;
    }

    // 2. 慢速路径：如果没见过这个实例，则计算它的内容哈希
    const contentString = JSON.stringify(obj);
    const key = simpleHash(contentString).toString();

    // 3. 将计算结果存入缓存，以便下次快速获取
    objectToKeyCache.set(obj, key);

    return key;
}


export const useRuneAnalysisStore = defineStore('runeAnalysis', () =>
{

    const analysisCache = ref<RuneAnalysisCache>({});

    const pendingRequests = ref<PendingRequestCache>({});

    async function _analyze(runeConfig: AbstractRuneConfig, runeId: string, tuumContext: TuumConfig | null = null)
    {
        // 使用包含/不包含上下文的完整配置作为缓存键，以区分不同上下文下的相同符文
        const configKey = getContentKey(runeConfig);
        const contextKey = getContentKey(tuumContext);
        const combinedKey = `${configKey}:${contextKey}`;

        if (analysisCache.value[combinedKey])
        {
            // 如果缓存中有，则直接返回
            return analysisCache.value[combinedKey];
        }

        if (combinedKey in pendingRequests.value)
        {
            return pendingRequests.value[combinedKey];
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
                analysisCache.value[combinedKey] = result;
                return result; // <--- 正确地返回结果，这将 resolve Promise
            } catch (error)
            {
                console.error(`在分析符文 ${runeId} 时失败: `, error);
                throw error; // <--- 正确地抛出错误，这将 reject Promise
            } finally
            {
                // 无论成功或失败，都从在途请求中移除
                delete pendingRequests.value[combinedKey];
            }
        })(); // <--- 最后的 () 表示立即执行

        // 将这个新创建的 Promise 存入在途请求映射中
        pendingRequests.value[combinedKey] = analysisPromise;

        // 返回这个 Promise，让调用方去 await
        return analysisPromise;
    }

    // 分析单个符文的动作，带有缓存功能
    const analyzeRune = _analyze;

    // 从缓存中获取分析结果的getter
    const getAnalysisResult = computed(() => (runeConfig: AbstractRuneConfig, tuumContext: TuumConfig | null = null) =>
    {
        const configKey = getContentKey(runeConfig);
        const contextKey = getContentKey(tuumContext);
        const combinedKey = `${configKey}:${contextKey}`;
        return analysisCache.value[combinedKey];
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