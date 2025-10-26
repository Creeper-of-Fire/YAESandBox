import { onMounted, ref, type Ref, watch } from "vue";
import type { WorkflowConfig, WorkflowValidationReport } from "#/types/generated/workflow-config-api-client";
import { useWorkflowAnalysisStore } from "#/stores/useWorkflowAnalysisStore";
import { useDebounceFn } from "@vueuse/core";

/**
 * 一个 Vue Composable，用于对工作流（Workflow）配置进行实时分析。
 * @param workflow - 一个包含 WorkflowConfig 的响应式引用 (Ref)。
 * @returns 返回包含分析报告、加载状态和刷新方法的对象。
 */
export function useWorkflowAnalysis(workflow: Ref<WorkflowConfig | null>) {
    const workflowAnalysisStore = useWorkflowAnalysisStore();
    const analysisReport = ref<WorkflowValidationReport | null>(null);
    const isLoading = ref(false);

    // 使用 vueuse 的 useDebounceFn 创建一个防抖函数，以避免在用户快速编辑时过于频繁地调用API
    const debouncedExecuteAnalysis = useDebounceFn(executeAnalysis, 500); // 500ms的延迟

    /**
     * 执行分析的核心函数。
     * @param newWorkflow - 最新的工作流配置数据。
     */
    async function executeAnalysis(newWorkflow: WorkflowConfig | null) {
        if (!newWorkflow) {
            analysisReport.value = null;
            return;
        }

        isLoading.value = true;
        try {
            // 调用 store 中的分析方法
            analysisReport.value = await workflowAnalysisStore.analyzeWorkflow(newWorkflow) || null;
        } catch (error) {
            console.error(`[useWorkflowAnalysis] 在分析工作流 ${newWorkflow.name} 时失败: `, error);
            analysisReport.value = null; // 失败时清空结果
        } finally {
            isLoading.value = false;
        }
    }

    // 深度监听 workflow Ref 的变化，并在变化时触发防抖的分析函数
    watch(workflow, (newWorkflow) => {
        // 当工作流变化时，调用防抖函数
        debouncedExecuteAnalysis(newWorkflow);
    }, {
        deep: true,       // 深度监听，因为 WorkflowConfig 是一个复杂的对象
        immediate: false  // 初始时不立即执行，由 onMounted 处理
    });

    // 组件挂载时，立即执行一次分析
    onMounted(() => {
        executeAnalysis(workflow.value);
    });

    return {
        /**
         * 工作流分析报告的响应式引用。
         */
        analysisReport,
        /**
         * 指示当前是否正在进行分析的布尔值。
         */
        isLoading,
        /**
         * 手动触发一次分析的函数（绕过防抖）。
         */
        refresh: () => executeAnalysis(workflow.value),
    };
}