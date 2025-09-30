import { ref, computed, inject } from 'vue';
import type { Ref } from 'vue';

type TestResponseDto = {
    isSuccess: boolean;
    result: any;
    errorMessage: string;
    debugInfo: any;
}

// 接收整个符文的配置 (formValue) 和测试输入文本
export function useRuneTester(runeConfig: Ref<any>, sampleInput: Ref<string>) {
    const axios = inject('axios') as any;

    const isLoading = ref(false);
    const testResult = ref<any>(null);
    const testError = ref('');
    const testDebugInfo = ref<any>(null);

    const formattedResult = computed(() => {
        if (testResult.value === null) return '';
        if (typeof testResult.value === 'object') {
            return JSON.stringify(testResult.value, null, 2);
        }
        return String(testResult.value);
    });

    const formattedDebugInfo = computed(() => testDebugInfo.value ? JSON.stringify(testDebugInfo.value, null, 2) : '');

    async function runTest(testApiEndpoint: string) {
        if (!axios) {
            testError.value = "错误：未能获取到 axios 实例。";
            return;
        }
        isLoading.value = true;
        testResult.value = null;
        testError.value = '';
        testDebugInfo.value = null;

        const requestPayload = {
            runeConfig: runeConfig.value,
            // 根据输入类型，决定发送什么样本数据
            // 注意：这里我们简化为只测试字符串输入，因为 PromptList 测试会更复杂
            SampleInputText: sampleInput.value
        };

        try {
            const response: { data: TestResponseDto } = await axios.post(testApiEndpoint, requestPayload);
            const data = response.data;

            if (data.isSuccess) {
                testResult.value = data.result;
            } else {
                testError.value = data.errorMessage || '未知错误';
            }
            testDebugInfo.value = data.debugInfo;
        } catch (error: any) {
            testError.value = error.response?.data?.detail || error.response?.data?.title || error.message || '请求失败';
        } finally {
            isLoading.value = false;
        }
    }

    return {
        isLoading,
        testResult,
        testError,
        testDebugInfo,
        formattedResult,
        formattedDebugInfo,
        runTest,
    };
}