// src/composables/useRuneTester.ts
import {ref} from 'vue';
import type {AbstractRuneConfig, MockRunResponseDto} from '#/types/generated/workflow-config-api-client';
import {MockRuneService} from '#/types/generated/workflow-config-api-client';
import {ApiError} from "#/types/generated/workflow-config-api-client";

/**
 * 一个 Vue Composable, 用于执行符文的模拟运行测试。
 */
export function useRuneTester()
{
    const result = ref<MockRunResponseDto | null>(null);
    const isLoading = ref(false);
    const error = ref<string | null>(null);

    /**
     * 执行测试。
     * @param runeConfig - 要测试的符文的完整配置。
     * @param mockInputs - 一个包含模拟输入值的 Record 对象。
     */
    async function executeTest(runeConfig: AbstractRuneConfig, mockInputs: Record<string, any>)
    {
        isLoading.value = true;
        error.value = null;
        result.value = null;

        try
        {
            // 在发送前，对 mockInputs 的值进行 JSON.parse，因为用户可能在 textarea 中输入了 JSON 字符串
            const parsedInputs: Record<string, any> = {};
            for (const key in mockInputs)
            {
                try {
                    // 尝试解析，如果不是合法的JSON字符串（比如普通数字或文本），则直接使用原始值
                    parsedInputs[key] = JSON.parse(mockInputs[key]);
                } catch (e) {
                    parsedInputs[key] = mockInputs[key];
                }
            }

            result.value = await MockRuneService.postApiV1WorkflowRuneMockRun({
                requestBody: {
                    runeConfig,
                    mockInputs: parsedInputs,
                }
            });
        }
        catch (e)
        {
            if (e instanceof ApiError) {
                // 尝试解析 body 中的 ProblemDetails
                const errorBody = e.body as { title?: string, detail?: string };
                error.value = errorBody?.detail || errorBody?.title || e.message;
            } else {
                error.value = (e as Error).message;
            }
            console.error('[useRuneTester] 测试执行失败:', e);
        }
        finally
        {
            isLoading.value = false;
        }
    }

    return {
        result,
        isLoading,
        error,
        executeTest,
    };
}