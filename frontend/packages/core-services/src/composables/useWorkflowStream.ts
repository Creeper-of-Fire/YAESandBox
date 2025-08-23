import { ref, inject, onScopeDispose, type Ref } from 'vue';
import { executeWorkflowStream } from '../services/workflowService';
import { TokenResolverKey } from '../utils/injectKeys';
import type {WorkflowConfig} from "../types";

/**
 * 一个可复用的 Vue Composable，用于执行后端工作流并处理流式 JSON 响应。
 * @template T 预期从流中解析出的最终对象类型。
 */
export function useWorkflowStream<T>() {
    // --- 响应式状态 ---

    // 存储流式解析出的数据。它会在接收到新数据时不断更新。
    const data: Ref<T | null> = ref(null);
    // 指示流式请求是否正在进行中。
    const isLoading = ref(false);
    // 存储请求过程中发生的任何错误。
    const error: Ref<Error | null> = ref(null);
    // 指示流是否已成功完成。
    const isFinished = ref(false);

    // --- 内部状态 ---

    // 用于持有中止函数，以便在组件卸载时中止请求。
    let abortController: { abort: () => void } | null = null;

    // --- 依赖注入 ---

    // 从上层提供者注入 token 解析器。这是与主应用认证系统解耦的关键。
    const tokenResolver = inject(TokenResolverKey);

    // --- 核心方法 ---

    /**
     * 执行工作流。
     * @param workflowConfig 要执行的工作流的配置。
     * @param workflowInputs 工作流所需的输入参数。
     */
    async function execute(workflowConfig: WorkflowConfig, workflowInputs: Record<string, string>) {
        if (!tokenResolver) {
            const err = new Error('TokenResolver not provided. Make sure it is injected in the application.');
            console.error(err);
            error.value = err;
            return;
        }

        // 1. 重置状态
        isLoading.value = true;
        isFinished.value = false;
        error.value = null;
        data.value = null;

        try {
            // 2. 调用核心服务，并传入回调函数来处理流式事件
            abortController = await executeWorkflowStream(
                { workflowConfig, workflowInputs },
                {
                    // onMessage: 每次从服务器收到一段数据时调用
                    onMessage: (content) => {
                        try {
                            // 假设后端发送的每个消息都是一个完整的、可解析的JSON字符串
                            // 它代表了当前对象的最新状态。
                            data.value = JSON.parse(content);
                        } catch (e) {
                            console.error('Failed to parse streaming JSON:', e);
                            // 如果解析失败，我们可以选择性地设置一个错误状态
                            error.value = new Error(`JSON parsing error: ${e}`);
                        }
                    },
                    // onError: 当流处理过程中发生错误时调用
                    onError: (err) => {
                        console.error('Workflow stream error:', err);
                        error.value = err;
                        isLoading.value = false;
                    },
                    // onClose: 当服务器发送 'done' 消息，表示流成功结束时调用
                    onClose: () => {
                        isLoading.value = false;
                        isFinished.value = true;
                        console.log('Workflow stream finished successfully.');
                    }
                },
                tokenResolver,
                'Json' // 明确指定使用 JSON 输出格式
            );
        } catch (e) {
            const err = e as Error;
            console.error('Failed to start workflow stream:', err);
            error.value = err;
            isLoading.value = false;
        }
    }

    /**
     * 手动中止流式请求。
     */
    function abort() {
        if (abortController) {
            abortController.abort();
            isLoading.value = false;
            console.log('Workflow stream aborted by user.');
        }
    }

    // 使用 onScopeDispose 确保当组件卸载时，自动中止正在进行的流式请求，防止内存泄漏。
    onScopeDispose(() => {
        abort();
    });

    // --- 返回 ---
    return {
        data,
        isLoading,
        error,
        isFinished,
        execute,
        abort,
    };
}