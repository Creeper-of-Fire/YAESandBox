// frontend/src/app-workbench/stores/useTuumAnalysisStore.ts
import {defineStore} from 'pinia';
import {ref} from 'vue';
import type {TuumConfig, TuumAnalysisResult} from '#/types/generated/workflow-config-api-client';
import {WorkflowAnalysisService} from '#/types/generated/workflow-config-api-client';

/**
 * @internal
 * 缓存接口，用于存储已完成的分析结果。
 *键是字符串化的 TuumConfig，值是分析结果。
 */
interface TuumAnalysisCache {
    [key: string]: TuumAnalysisResult;
}

/**
 * @internal
 * 在途请求缓存接口，用于防止对同一配置的重复请求。
 */
interface PendingRequestCache {
    [configString: string]: Promise<TuumAnalysisResult | undefined>;
}

/**
 * 一个专门用于枢机（Tuum）分析的 Pinia Store。
 * 它负责调用后端的 analyze-tuum 端点，并对结果进行缓存，以提高性能。
 */
export const useTuumAnalysisStore = defineStore('tuumAnalysis', () => {
    // 状态：存储分析结果的缓存
    const analysisCache = ref<TuumAnalysisCache>({});
    // 状态：存储正在进行中的请求Promise
    const pendingRequests = ref<PendingRequestCache>({});

    /**
     * 分析单个枢机配置。
     * @param tuumConfig - 要分析的枢机配置对象。
     * @returns 返回一个 Promise，该 Promise 解析为分析结果。
     */
    async function _analyze(tuumConfig: TuumConfig) {
        const configString = JSON.stringify(tuumConfig);

        // 1. 检查缓存中是否已有结果
        if (analysisCache.value[configString]) {
            return analysisCache.value[configString];
        }

        // 2. 检查是否有正在进行的相同请求
        if (configString in pendingRequests.value) {
            return pendingRequests.value[configString];
        }

        // 3. 发起新的分析请求
        const analysisPromise = (async () => {
            try {
                const result = await WorkflowAnalysisService.postApiV1WorkflowsConfigsAnalysisAnalyzeTuum({
                    requestBody: tuumConfig,
                });
                // 请求成功，存入缓存
                analysisCache.value[configString] = result;
                return result;
            } catch (error) {
                console.error(`在分析枢机 ${tuumConfig.name} (${tuumConfig.configId}) 时失败: `, error);
                throw error; // 向上抛出错误，让调用方处理
            } finally {
                // 无论成功或失败，都从在途请求中移除
                delete pendingRequests.value[configString];
            }
        })();

        // 将这个新创建的 Promise 存入在途请求映射中
        pendingRequests.value[configString] = analysisPromise;

        // 返回 Promise
        return analysisPromise;
    }

    const analyzeTuum = _analyze;

    /**
     * 清除所有分析缓存。
     */
    const clearCache = () => {
        analysisCache.value = {};
    };

    return {
        analyzeTuum,
        clearCache,
    };
});