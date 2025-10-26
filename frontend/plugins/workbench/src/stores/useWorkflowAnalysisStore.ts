import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { WorkflowConfig, WorkflowValidationReport } from '#/types/generated/workflow-config-api-client';
import { WorkflowAnalysisService } from '#/types/generated/workflow-config-api-client';

/**
 * @internal
 * 缓存接口，用于存储已完成的工作流分析结果。
 * 键是字符串化的 WorkflowConfig，值是分析报告。
 */
interface WorkflowAnalysisCache {
    [key: string]: WorkflowValidationReport;
}

/**
 * @internal
 * 在途请求缓存接口，用于防止对同一工作流配置的重复分析请求。
 */
interface PendingRequestCache {
    [configString: string]: Promise<WorkflowValidationReport | undefined>;
}

/**
 * 一个专门用于工作流（Workflow）级别分析的 Pinia Store。
 * 它负责调用后端的 analyze-workflow 端点，并对结果进行缓存。
 */
export const useWorkflowAnalysisStore = defineStore('workflowAnalysis', () => {
    // 状态：存储分析结果的缓存
    const analysisCache = ref<WorkflowAnalysisCache>({});
    // 状态：存储正在进行中的请求Promise
    const pendingRequests = ref<PendingRequestCache>({});

    /**
     * 分析单个工作流配置。
     * @param workflowConfig - 要分析的工作流配置对象。
     * @returns 返回一个 Promise，该 Promise 解析为分析报告。
     */
    async function _analyze(workflowConfig: WorkflowConfig): Promise<WorkflowValidationReport | undefined> {
        const configString = JSON.stringify(workflowConfig);

        // 1. 检查缓存中是否已有结果
        if (analysisCache.value[configString]) {
            console.log("日志: 从缓存中命中工作流分析结果。");
            return analysisCache.value[configString];
        }

        // 2. 检查是否有正在进行的相同请求
        if (configString in pendingRequests.value) {
            console.log("日志: 等待一个正在进行中的工作流分析请求。");
            return pendingRequests.value[configString];
        }

        // 3. 发起新的分析请求
        console.log("日志: 发起新的工作流分析请求。");
        const analysisPromise = (async () => {
            try {
                const result = await WorkflowAnalysisService.postApiV1WorkflowsConfigsAnalysisValidateWorkflow({
                    requestBody: workflowConfig,
                });
                // 请求成功，存入缓存
                analysisCache.value[configString] = result;
                console.log("日志: 工作流分析成功，结果已缓存。");
                return result;
            } catch (error) {
                console.error(`在分析工作流 ${workflowConfig.name} 时失败: `, error);
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

    const analyzeWorkflow = _analyze;

    /**
     * 清除所有分析缓存。
     */
    const clearCache = () => {
        analysisCache.value = {};
        console.log("日志: 工作流分析缓存已清除。");
    };

    return {
        analyzeWorkflow,
        clearCache,
    };
});