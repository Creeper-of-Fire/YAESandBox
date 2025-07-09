import {defineStore} from 'pinia';
import {computed, ref} from 'vue';
import type {AbstractModuleConfig, ModuleAnalysisResult} from '@/app-workbench/types/generated/workflow-config-api-client';
import {WorkflowAnalysisService} from '@/app-workbench/types/generated/workflow-config-api-client';

interface ModuleAnalysisCache
{
    [key: string]: ModuleAnalysisResult;
}

interface PendingRequestCache
{
    [configString: string]: Promise<ModuleAnalysisResult | undefined>;
}

export const useModuleAnalysisStore = defineStore('moduleAnalysis', () =>
{

    const analysisCache = ref<ModuleAnalysisCache>({});

    const pendingRequests = ref<PendingRequestCache>({});

    async function _analyze(moduleConfig: AbstractModuleConfig, moduleId: string)
    {
        const configString = JSON.stringify(moduleConfig); // 使用字符串化的配置作为缓存键

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
                const result = await WorkflowAnalysisService.postApiV1WorkflowsConfigsAnalysisAnalyzeModule({
                    requestBody: moduleConfig,
                });
                // 请求成功，存入缓存
                analysisCache.value[configString] = result;
                return result; // <--- 正确地返回结果，这将 resolve Promise
            } catch (error) {
                console.error(`在分析模块 ${moduleId} 时失败: `, error);
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

    // 分析单个模块的动作，带有缓存功能
    const analyzeModule = _analyze;

    // 从缓存中获取分析结果的getter
    const getAnalysisResult = computed(() => (moduleConfig: AbstractModuleConfig) =>
    {
        const configString = JSON.stringify(moduleConfig);
        return analysisCache.value[configString];
    });

    // 清除缓存的动作（例如当工作流发生重大变化时）
    const clearCache = () =>
    {
        analysisCache.value = {};
    };

    return {
        analysisCache,
        analyzeModule,
        getAnalysisResult,
        clearCache,
    };
});